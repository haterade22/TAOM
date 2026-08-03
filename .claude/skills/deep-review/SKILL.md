---
name: deep-review
description: Launch parallel deep-dive agents to review completed work for quality, standards, compatibility, completeness, and cross-system data flow
argument-hint: "[feature-name]"
effort: high
---

# Deep Review

Launch as many review agents as needed to audit the current session's work. The baseline is 5 core agents (below), but there is NO LIMIT — if the scope demands 10, 20, or 100 agents, launch them. Scale the review to match the risk.

Run this AFTER completing a feature or fix, BEFORE closing out.

The feature or area to review: `$ARGUMENTS` (if empty, review all uncommitted changes).

## Step 0 (Optional): Codex Independent Pre-Review

**Trigger when:** `$ARGUMENTS` contains `--codex` (strip `--codex` from the feature name before proceeding).

If triggered:
1. Identify changed files (same logic as Step 1 below).
2. **Dispatch Codex directly via Bash** -- Claude does this itself, no terminal hand-off:
   - Pre-flight: `codex login status` -- expect `Logged in using ChatGPT`. If not, surface the message and continue WITHOUT the Codex pre-review (don't block the Claude agents).
   - Write a focused prompt to `docs/reviews/codex-prereview-{feature}-{date}.prompt.md` (short version of the `/review-codex` prompt -- focus on Known Suspects + architectural risks; skip the heavy vanilla-decompile block).
   - Run via Bash:
     ```
     command: cd "<repo-root>" && mkdir -p docs/reviews/raw && codex exec -c project_doc_max_bytes=65536 - < "docs/reviews/codex-prereview-{feature}-{date}.prompt.md" > "docs/reviews/raw/codex-prereview-{feature}-{date}.md" 2>&1
     run_in_background: true
     timeout: 600000
     ```
   - See `.claude/skills/review-codex/SKILL.md` "Codex CLI invocation contract" for full dispatch semantics.
3. Continue to Step 1 immediately — do NOT wait for Codex here. The 5 Claude agents run in parallel with the Codex background job.
4. After all 5 Claude agents complete (Step 2), check if the Codex background job has notified. If yes, read `docs/reviews/raw/codex-prereview-{feature}-{date}.md`. If not yet (Claude agents finish faster on this kind of work), Codex result will arrive later -- proceed with Step 3 using just the Claude agent results and append Codex when it arrives.
5. Include Codex findings in the Step 3 compiled report as a sixth section:
   ```
   CODEX REVIEW:  [PASS/ISSUES — N findings]
   [Codex findings grouped by severity]
   ```

If Codex and any Claude agent disagree on a finding, flag the disagreement explicitly — it is valuable signal.

If `--codex` is not present, skip this step entirely.

## Step 1: Identify Scope

Determine what to review:
- If `$ARGUMENTS` is provided, focus on that feature/area
- Otherwise, use `git diff --name-only` and `git ls-files --others --exclude-standard` to find all changed/new files

Collect the list of changed files for the agents.

## Step 2: Launch Core Review Agents in Parallel

Launch ALL core agents in a SINGLE message (parallel execution). Pass each agent the list of changed files.

**Minimum: 5 core agents (always launch all 5).** If the changeset spans multiple features, multiple XML config files, multiple Harmony patches, or touches more than 20 files — launch ADDITIONAL focused agents for each distinct subsystem. There is no upper limit on agent count. The cost of missing a bug in production is always higher than the cost of an extra review agent.

**Triage ordering — spec compliance before code quality** (per `docs/ai-includes/agent-teams.md` "Subagent review ordering"): launch all agents in parallel as above, but when triaging the returned findings, resolve Agent 1 (Standards Compliance — ADR breaches, direct TaleWorlds usage in services, registration gaps) and spec/data-flow findings BEFORE acting on Agent 3 (Efficiency) quality findings. A standards violation can make quality feedback moot — don't optimize code that's about to be restructured.

### Agent 1: Standards Compliance

```
subagent_type: Explore
model: haiku
```

**Prompt:**
```
Review these files for TAOM project standards compliance. Read each file and check:

FILES: [list changed files]

CHECK ALL OF THESE:
1. **Adapter Pattern (ADR-007):** Services NEVER reference TaleWorlds types directly (Hero, Clan, Kingdom, etc.). They use IXxxAdapter interfaces. Flag ANY direct TaleWorlds type usage in service classes.
2. **No #region (ADR-003):** Zero `#region` directives anywhere.
3. **No [Obsolete] (ADR-004):** Zero `[Obsolete]` attributes.
4. **No #if DEBUG (ADR-005):** Zero preprocessor directives except in IoC.cs.
5. **Thin Entry Points (ADR-002):** Behaviors/Models/Patches under 150 lines AND they delegate to services. Line count is a ceiling, NOT the test. For each GameModel override in the changeset, inspect the override method body: if it contains `if`, `foreach`, `switch`, `yield return` with branching, or any multi-line decision logic INLINE (not inside a service call), that is a violation — even if the file is under 20 lines. The only acceptable override bodies are (a) a single constant/expression (`=> 10`), or (b) boundary conversion (adapter wrap, perk check) plus a direct delegate to an injected service. Do NOT invent "simple enough to skip the service" carve-outs; they are not in the rules.
6. **Interface Segregation:** Every service has an interface. Every adapter has an interface.
7. **IoC Registration:** New services/adapters registered in the feature's IoC.cs or Main/IoC.cs.
8. **Naming:** Classes match file names. Interfaces prefixed with I.
9. **No Service Locator (Constructor Injection Only):** Flag ANY `IoC.Resolve<T>()` or `IoC.ResolveAll<T>()` call outside a BOUNDARY class. The only acceptable boundary locations are: (a) Harmony patch static methods, (b) `ScreenBase` subclasses and other TaleWorlds-constructed entry points, (c) `CampaignBehaviorBase` constructors, (d) `GameModel` constructors, (e) `SubModule.cs`, (f) static `OpenXxx()` helpers that exist because the caller has no DI access. Services, ViewModels, engines, helpers, mixins, and hooks MUST receive dependencies via constructor injection. **Red flag patterns to grep for:** `IoC.Resolve<` inside any `.cs` file under `Main/Features/**/Services/`, `Main/Features/**/*Service.cs`, `Main/Features/**/*VM.cs` (excluding the VM's boundary parent), `Main/Features/**/Engines/`, or any method not in the boundary list above. A `try { IoC.Resolve<T>() } catch { }` guard is STILL a violation — it merely hides the test-time NRE while preserving the anti-pattern. **Why this rule exists:** Review #26 (2026-04) — 8 CareerScreenVMTests failed with NullReferenceException because `CareerScreenVM` resolved `ICareerConfigProvider` and `IModLogger` via service locator. DryIoc isn't configured in unit tests, so every test that exercised the code path threw. The rule was already in `.claude/rules/csharp-architecture.md` but wasn't mechanized here, so it wasn't caught at review time.
9b. **C++ Native Hook Standards (only if `.cpp` / `.h` files in scope — e.g., `Dependencies/*.NativeHooks/`, `Main/SceneScripts/`, or other vendored native code).** TAOM C++ ports follow these baselines; flag violations with HIGH severity when they affect runtime safety, MEDIUM otherwise:
    - **`extern "C"` block** around every `__declspec(dllexport)` function — otherwise the export gets a C++-mangled name and the C# `GetProcAddress` call returns null.
    - **Calling convention** explicitly stated (`__cdecl`, `__fastcall`, `__stdcall`) on every exported function declaration AND definition. The C# side declares the convention on the delegate; the C++ side MUST match — otherwise the stack is corrupted on call.
    - **`#pragma once`** at the top of every header (preferred over include guards for new TAOM C++ files).
    - **No `using namespace std;`** at file scope in headers (fine in .cpp).
    - **No catch-all SEH filters** — `__except (EXCEPTION_EXECUTE_HANDLER)` without a `GetExceptionCode()` check is rejected. See Agent 3's "SEH filter overbreadth" rule for the canonical pattern.
    - **No hot-path I/O without sample gating** — see Agent 3 for the canonical pattern.
    - **Exception specifications** (`throw()`, `noexcept`) — `extern "C"` functions called from C# P/Invoke should not allow C++ exceptions to escape. Mark with `noexcept` where appropriate, or wrap the body in a try/catch that converts to error-return.
    - **Inheritance / port discipline:** if any C++ file was copied from an upstream mod, the port must be audited from scratch per `feedback_native_port_hot_path_audit.md` — don't trust "upstream worked." Specifically check that hot-path logging, SEH filters, lock balance, and atomic counter usage all meet TAOM standards (Agent 3 has the full list). Reference: RCA `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`.

10. **Custom GauntletLayer Input Wiring (MANDATORY for any `new GauntletLayer(...)` with interactive widgets):** Flag any new `GauntletLayer` instantiation in **any feature-overlay attach path** that does NOT call `_layer.InputRestrictions.SetInputRestrictions()` after construction. Attach paths to cover: (a) Harmony postfix on `<ScreenBase>.OnInitialize` (e.g., `GauntletInventoryScreen`, `GauntletPartyScreen`, `GauntletEncyclopediaScreen`), (b) `MissionView.OnMissionScreenInitializeFirstTime` or `MissionView`-derived attach methods (e.g., the OOB and BattleActionBar overlays), (c) `MissionLogic` overlay attach, (d) MCM-triggered overlay attach helpers, (e) any service method invoked from one of the above. The v1.4.5 input dispatcher does NOT distinguish between `ScreenBase` and `MissionScreen` hosts — both require `SetInputRestrictions()` on layers with interactive widgets, or mouse clicks pass through silently. Also flag if the teardown path (`OnFinalize_Prefix`, `OnMissionScreenFinalize`, `Detach`, etc.) calls `RemoveLayer` WITHOUT first calling `_layer.InputRestrictions.ResetInputRestrictions()`. **Exception:** display-only overlays with zero interactive widgets (no `ButtonWidget`, no `Command.Click`, no `AcceptEvents` matches in the prefab XML — e.g., CareerSystem `AbilityHUD`) can omit `SetInputRestrictions()`. Verify the exception via grep on the prefab before claiming it; a single stray `Command.Click` revokes the exception. **Full-screen replacement screens** (subclasses of `ScreenBase` that ARE the screen, not parasitic overlays — e.g., `GauntletCareerScreen`, `GauntletFiefManagementScreen`) follow the same Set/Reset rule AND set `IsFocusLayer = true`. **For parasitic overlays on top of a still-live vanilla screen (ScreenBase OR MissionScreen), NEVER set `IsFocusLayer = true`** — it steals Esc/Tab/hotkey focus from vanilla. The overlay's prefab parent widget should have `DoNotAcceptEvents="true"` so non-button areas pass clicks through. **Why this rule exists:** Two shipping bugs in 6 days — #202 EquipPresets (ScreenBase overlay, 2026-05-19) and #225 CompanionTactics OOB/BattleActionBar (MissionScreen overlays, 2026-05-25). Bug #2 shipped past the rule written for bug #1 because the rule was scoped to ScreenBase only — based on the wrong inference that BattleActionBar "worked" without `SetInputRestrictions()`. It didn't work; only its hotkey path did. **When classifying a sibling as a working precedent, verify it works via the SAME input path you care about — a working alternative input path is not evidence the broken path also works.** RCAs: `docs/reviews/rca-equippresets-presets-button-silent-2026-05-19.md` + `docs/reviews/rca-companiontactics-overlay-input-2026-05-25.md`. Feedback memory: `feedback_gauntlet_overlay_input_wiring.md`. Rule in `.claude/rules/gui-ui.md` → "Custom GauntletLayer Input Wiring."

OUTPUT FORMAT:
For each violation found:
- File path and line number
- Rule violated
- What needs to change

If all checks pass, say "ALL STANDARDS CHECKS PASSED" with a brief summary of what was reviewed.
```

### Agent 2: Bannerlord API Compatibility

```
subagent_type: taleworlds-researcher
model: sonnet
```

**Prompt:**
```
Review these files for Bannerlord API compatibility. Focus on TaleWorlds API usage.

CRITICAL: The decompiled source at E:\Decompiled_Bannerlord\ is from a DIFFERENT version than the installed game. ALWAYS verify against the INSTALLED DLLs using ilspycmd:
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "Full.Type.Name"
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "Full.Type.Name"
NEVER trust the decompiled folder for signature verification.

FILES: [list changed files]

FOR EACH FILE that references TaleWorlds APIs:
1. Identify every TaleWorlds class, method, property, or enum used
2. Decompile the relevant TaleWorlds type from the INSTALLED DLL to verify:
   - The method/property EXISTS
   - The SIGNATURE matches (parameter types, return type)
   - The method is not marked internal/private
   - For GameModel overrides: the base class method signature is correct
   - For Harmony patches: the target method exists with the expected signature
3. **SHARED-ENGINE-TYPE CHECK (MANDATORY when a generic template/class instantiates ONE engine type for MANY logical config variants — e.g. one IssueBase subclass for N issue configs, one MissionBehavior for N spawns).** The engine's `GetType()`-keyed bookkeeping collapses all variants into a single object. Decompile the engine BASE type + its manager/behavior and grep for EVERY path that branches on the runtime type: `GetType()`, `.GetType() ==`, `is <Type>`, `Dictionary<Type,...>`, type-name cooldown keys. Enumerate ALL of them and confirm the collapsed-to-one-type behavior is acceptable for each — do NOT stop at the first one found. The classic miss (Codex review #61): `IssueBase.CheckPreconditions` has TWO type-keyed gates in one method — a soft spawn-over-representation score AND a HARD accept gate (`IssueQuestCanBeDuplicated`, default false, caps the player at one active quest per type). The review found the soft one and shipped the hard one. For IssueBase the full set is: spawn score + per-settlement zero-out + accept gate + cooldown + despawn. See `.claude/rules/csharp-architecture.md` "One Engine Type for Many Config Variants."

OUTPUT FORMAT:
For each API usage:
- ✅ Verified: [Type.Method] — exists with matching signature
- ❌ INCOMPATIBLE: [Type.Method] — [reason: removed/renamed/signature changed]
- ⚠️ UNVERIFIED: [Type.Method] — could not decompile, needs manual check

Summary: X verified, Y incompatible, Z unverified
```

### Agent 3: Efficiency & Performance

```
subagent_type: Explore
model: haiku
```

**Prompt:**
```
Review these files for performance and efficiency issues. Read each file and check:

FILES: [list changed files]

**BEFORE YOU ASSERT OR DEFER ON ANY ENGINE CALL'S COST: DECOMPILE IT.** You have the same tools Agent 2 does — `pwsh tools/taom-src.ps1 path <FullTypeName>`, or `ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/<Assembly>.dll" -t "Full.Type.Name"`. A TaleWorlds method that looks like a plain accessor may allocate: `FaceGen.GetRaceNames()` returns `(string[])_raceNamesArray.Clone()` — a fresh array per call — while the adjacent `FaceGen.GetBaseMonsterNameFromRace(int)` indexes the same array for free. Names do not tell you this; bodies do. Look for `.Clone()`, `.ToArray()`, `.ToList()`, `new`, and string building inside anything you are about to call cheap or expensive.
- **An unverified cost claim is reported as UNVERIFIED, never as HIGH.** Severity requires evidence you actually read. Where a log or measurement exists, prefer it over estimation — do not derive a latency figure from an assumed syscall.
- **Before recommending a log-level downgrade (INFO → DEBUG), state what happens to that line in a hard crash.** TAOM's `FileLogger` drains INFO synchronously with a flush on the calling thread and leaves DEBUG on an async queue, deliberately, so a native CTD preserves the tail — and `_logFile` is a `StreamWriter`, whose `Flush()` goes to the OS file cache, NOT to disk (it never calls `FlushFileBuffers`). Read `Main/Core/Logging/FileLogger.cs` before costing any logging change. Downgrading a crash-localisation stamp destroys the stamp's purpose while looking like an optimisation. (2026-08-03: an agent recommended exactly this on the strength of an assumed disk-sync cost; measured reality was 1287 durable stamps in 145 ms, ~0.5% of load. RCA `docs/reviews/rca-battleload-agentbuild-2026-08-03.md`.)

CHECK ALL OF THESE:
1. **Hot Path Allocations:** Any code in DailyTick, HourlyTick, or OnTick handlers — avoid LINQ, avoid allocating lists/arrays per tick, use cached collections
2. **IoC.Resolve in Hot Paths:** Flag ANY IoC.Resolve<T>() call inside per-frame, per-hit, or per-tick methods. These MUST use lazy-cached properties instead.
3. **LINQ in Loops:** Flag .ToList(), .ToArray(), .Where().Select() chains inside loops or frequent callbacks
4. **String Concatenation:** Use string interpolation or StringBuilder, not repeated + concatenation
5. **Dictionary Lookups:** Use TryGetValue instead of ContainsKey + indexer (double lookup)
6. **Unnecessary Boxing:** Watch for value types passed as object parameters
7. **Caching Opportunities:** Repeated expensive lookups that could be cached (e.g., race lookups, config reads)
8. **IEnumerable Multiple Enumeration:** Flag any IEnumerable parameter that's enumerated more than once
9. **Closure Allocations in Loops:** Flag lambda/delegate creation inside per-frame loops (RemoveAll with closure, etc.)
10. **Resource Disposal:** IDisposable types properly disposed or in using blocks
11. **Harmony Patch Overhead:** For every `[HarmonyPatch]` class in changed files:
    - Check if the patch target is a hot method (called per-frame, per-tick, per-hit, per-AI-decision)
    - Flag any `IoC.Resolve` not using lazy-cached `??=` pattern
    - Flag any `new List<>`, `new Dictionary<>`, or LINQ chain inside the patch method body
    - Flag any delegate/closure creation that captures local variables
12. **CampaignBehavior Lifecycle Cleanup:** For every `CampaignBehaviorBase` subclass in changed files:
    - Check that `RegisterEvents` has a corresponding cleanup path
    - Flag behaviors that subscribe to events in `OnSessionLaunched` but don't override `OnFinalize` or `OnGameEnd` to unsubscribe
    - Flag any static fields or `static Dictionary` that are populated at runtime but never cleared on session end
    - Flag any collection (List, Dictionary, HashSet) used for persistence/sync/tracking that grows with game events but has no pruning, eviction, or size cap — especially SyncData stores, buff trackers, and per-hero caches
13. **GameModel Override Weight:** For every `GameModel` override in changed files:
    - Identify the override methods and assess call frequency (per-frame vs per-day vs one-time)
    - Flag any service resolution that isn't constructor-injected or lazy-cached
    - Flag any LINQ chain or collection allocation inside override methods called more than once per game tick
14. **GC Pressure Patterns:** Across all changed C# files:
    - Flag `string.Format` or `$""` interpolation inside loops (prefer StringBuilder for >3 concatenations)
    - Flag `params object[]` calls in tight loops (implicit array allocation)
    - Flag `foreach` over `Dictionary.Keys` or `.Values` when only one is needed and the dictionary is large
    - Flag `Enum.ToString()` or `Enum.Parse()` in hot paths (both allocate; prefer lookup dictionaries)

15. **C++ HOT-PATH CHECKS (only if `.cpp` / `.h` files in scope — e.g., `Dependencies/*.NativeHooks/`, `Main/SceneScripts/`, or any other vendored native code).** TAOM C++ runs alongside the engine's render / asset-load / per-agent / per-frame paths. Hot-path I/O or unbounded loops in C++ are AS DESTRUCTIVE as in C#, but the C# checks above don't translate. Treat the following as analogous to the C# hot-path rules and apply with HIGH severity when they fire on per-frame / per-render / per-asset-load / per-agent / per-Face_mesh callbacks:

    - **Logging on the hot path.** Flag ANY `OutputDebugStringA`, `OutputDebugStringW`, `fprintf`, `fputs`, `fputc`, `fflush`, `printf`, `WriteFile`, or wrapping `LogLine` / `LogToFile` / `Log*` helper call inside a function that fires per-frame or per-engine-callback. Each call typically takes a critical section + writes + flushes — thousands per battle load is a visible frame stutter. **Required pattern:** sample-gate with an atomic counter — `if (ShouldSampleLog(&counter)) Log...(...)` — and emit a single summary in the corresponding `Uninstall` / shutdown path. Whitelist: logging inside `*_Install` / `*_Uninstall` / one-time boot paths is fine; logging inside per-call hook bodies is not. Reference: `feedback_native_port_hot_path_audit.md`, RCA `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md` finding #1 (HIGH).

    - **SEH filter overbreadth.** Flag ANY `__except (EXCEPTION_EXECUTE_HANDLER)` block. The filter MUST narrow to specific expected exception classes via `GetExceptionCode()` — typically `EXCEPTION_ACCESS_VIOLATION` for raw pointer dereferences against engine structs. Catch-all filters silently swallow heap corruption, stack overflow, division by zero, and other lethal exceptions that should propagate to the OS crash dumper so we get a real crash report. **Required pattern:** `__except (GetExceptionCode() == EXCEPTION_ACCESS_VIOLATION ? EXCEPTION_EXECUTE_HANDLER : EXCEPTION_CONTINUE_SEARCH)`. Reference: `feedback_seh_filter_specificity.md`, RCA same file finding #2 (MEDIUM).

    - **Non-atomic counters touched from the hot path.** Any static `int` / `long` / `size_t` incremented from a hook body that can fire on multiple engine threads (render, asset-load, AI) must use `InterlockedIncrement64` against a `volatile LONG64`. Plain `++counter` from multiple threads is a data race — sample-log gating breaks silently if the counter rolls back.

    - **SRWLock reader/writer balance.** Verify shared queries use `AcquireSRWLockShared` + `ReleaseSRWLockShared`; mutations use `AcquireSRWLockExclusive` + `ReleaseSRWLockExclusive`. Flag any query path that takes the exclusive lock (false serialisation) or any mutation path that takes the shared lock (UB on concurrent writes).

    - **Unbounded memory iteration.** Flag any `for` / `while` loop walking module memory or large engine buffers without a known upper bound. Examples: `SignatureScanner::FindPattern` walking the whole loaded DLL is acceptable ONLY at boot (once per signature); a similar walk inside a per-frame callback is not. Confirm the call site fires once per process / once per scene / once per battle, not per-frame.

    - **`new` / `malloc` / unbounded `std::vector::push_back` in hook bodies.** Heap allocations on the hot path are equivalent to GC pressure in C#. Flag any `new`, `malloc`, `std::vector::push_back` (on a vector likely to grow), `std::unordered_set::insert` (rehash on grow), or `std::string` operation inside per-frame / per-engine-callback code.

    - **Thread-local storage abuse.** TLS reads (`__declspec(thread)`) are cheap-but-not-free. Flag a TLS access inside a tight loop where caching the value to a local would avoid repeated lookups.

    - **Pattern-scan match validation.** When a byte-pattern scanner returns a match, the consumer should verify the result lands inside an executable section (`.text`) and inside a function prologue (e.g., bytes that look like `push rbp` / `sub rsp, ...` / `mov [rsp+...]`). A 7-byte match in the middle of a data section will produce a JIT crash when MinHook tries to install a trampoline. Flag any consumer that uses the raw match address without sanity-checking it.

    Apply these C++ checks ONLY when `.cpp` / `.h` files are in the changeset. For pure-C# changesets, skip this entire block.

OUTPUT FORMAT:
For each issue found:
- File path and line number
- Issue type (allocation, LINQ, caching, patch overhead, lifecycle leak, GC pressure, C++ hot-path log spam, SEH overbreadth, etc.)
- Severity: HIGH (hot path / per-frame) / MEDIUM (per-tick / occasional) / LOW (startup only)
- Suggested fix

If no issues found, say "NO PERFORMANCE ISSUES FOUND" with a brief summary.
```

### Agent 4: Completeness Check

```
subagent_type: Explore
model: haiku
```

**Prompt:**
```
Review the current work session for completeness. Check ALL of these:

FILES: [list changed files]

1. **Tests Exist:** For every new service/behavior/model class in Main/Features/, verify a corresponding test file exists in TAOM.Tests/Features/. Flag any untested classes.
2. **Test Coverage:** Read each test file. Are edge cases covered? Are there tests for error/null/empty cases? Is the AAA pattern used (Arrange/Act/Assert)?
3. **Feature Doc:** If this is a new feature, check that docs/features/<name>.md exists. If not, flag it as MISSING.
4. **GitHub Issue:** Run `gh issue list --state all --limit 20` and check if there's an issue for this work. If not, flag as MISSING.
5. **CHANGELOG Updated:** Check if CHANGELOG.md has been modified with an entry for this work. Use `git diff --name-only -- CHANGELOG.md`.
6. **IoC Registered:** Check that new services/adapters are registered in DryIoc. Read the relevant IoC.cs file.
7. **SubModule.xml:** If new behaviors or models were added, verify they don't need SubModule.xml registration (most don't, but check).

OUTPUT FORMAT:
- ✅ Tests: [X test files, Y test methods]
- ✅/❌ Feature Doc: [exists at path / MISSING]
- ✅/❌ GitHub Issue: [#N title / MISSING]
- ✅/❌ CHANGELOG: [updated / NOT UPDATED]
- ✅/❌ IoC: [registered / MISSING registrations]

Overall: COMPLETE / INCOMPLETE — [list what's missing]
```

### Agent 5: Cross-System Data Flow Tracing

**This agent catches bugs that per-file reviews miss — where data declared in one file is consumed (or NOT consumed) in another.**

```
subagent_type: Explore
model: sonnet
```

**Prompt:**
```
CROSS-SYSTEM DATA FLOW REVIEW — trace data declarations through the codebase to find gaps where declared data is never consumed, or where parallel code paths use inconsistent logic.

FILES: [list changed files]

This review exists because per-file reviews consistently miss bugs that span multiple files. Every check below has caught real bugs in this project.

TRACE THESE DATA FLOWS:

1. **XML Config → C# Consumption:** For every configurable value declared in XML (ModuleData/**/*.xml), trace it to the C# code that reads and acts on it. Flag any XML attribute that is parsed but never used at runtime.
   - Read ALL changed XML files. For each attribute/element, grep the C# codebase for where it's consumed.
   - Example bug pattern: XML declares `charge_type="DamageDone"` but the only code that emits charges uses `ChargeType.Kills`.

2. **Enum Coverage:** For every enum type referenced in changed files, check that ALL enum values have at least one handler. Flag any enum value with zero callsites.
   - Example bug pattern: `PassiveEffectType` has 50 values but only 15 are wired into GameModels.
   - Example bug pattern: `ChargeType` has 5 values but only 1 is emitted by mission behavior.

2b. **MCM toggle coverage (MANDATORY — applies to EVERY toggle, not a hand-listed subset):** For every MCM `AttributeGlobalSettings<>` / `AttributePerCampaignSettings<>` derived class in the changed files, enumerate EVERY property (excluding metadata: Id/DisplayName/FormatType/FolderName/etc.) and grep the entire feature's source for read sites. For each property:
   - If the property has ZERO read sites, it's a dead toggle — flag as HIGH.
   - If the property has EXACTLY ONE read site at startup (`SubModule.OnSubModuleLoad`, `IoC.Configure`, a static initializer), AND the MCM hint text promises runtime behavior (words like "when off", "disables", "no-op", "stops"), the toggle promise mismatches the implementation — flag as HIGH (user-facing-promise pattern).
   - If the property has runtime read sites, verify each read actually gates behavior — a read-but-discard (e.g., logged but not branched on) is the same as dead.
   - **Master-toggle fold check (when any hint promises "off = vanilla/pre-feature behavior"):** enumerate EVERY override in the feature's GameModel(s)/patch(es) — including constant-returning getters like `GetXxx()` — and confirm each read path folds the master toggle (directly or via a settings-provider getter that folds it). A single unconditionally-read config value breaks the promise. Why: CombatMechanics (2026-07-02) — `GetHorseChargePenetration()` returned the TUNED config value with the feature toggled off; every other read folded the master. Caught only because the agent enumerated all 9 overrides instead of the "interesting" ones.
   - **Why this rule is rigid:** the deep-review Agent 5 on the CrashReport feature (2026-05-25) cross-referenced 5 of 6 toggles but missed the master toggle (`EnableCrashCapture`); Codex caught it as HIGH. The agent prompt previously listed toggles to check by name and missed the one not on the list. Generalised: enumerate from the class itself, not from a list in the prompt.

2c. **DTO non-empty-output trace (MANDATORY):** For every DTO collection field (`IReadOnlyList<T>`, `List<T>`, etc.) populated by a collector in the changed files, trace whether non-empty values are actually produced under normal operation — NOT just whether the populator runs. Specifically:
   - Find the populator method.
   - Trace every code path that adds items to the collection.
   - For each path, identify the precondition (parameter non-null, branch taken, etc.).
   - Check the caller(s): is the precondition actually satisfied by the caller in production?
   - If a collection field is structurally populated but the precondition is never met by any caller, the field is dead code (always empty) — flag as HIGH if the field appears in user-facing docs/CHANGELOG.
   - **Why this rule exists:** Codex review 41 (CrashReport, 2026-05-25) caught `HarmonyCorrelationCollector.Collect(stack, frames=null)` — the optional `frames` parameter controlled the per-stack-frame patch-info block; the sole caller skipped it; the renderer faithfully rendered empty lists in every report; "Harmony patches per stack frame" feature advertised in CHANGELOG was DEAD CODE. The 5 deep-review agents all passed and the test suite passed because no test covered the integration. Generalisation: "is the field populated?" is not the same as "are non-empty values actually produced?" — extend traces to ask the second question explicitly.

3. **Mutation/Transform Chain Completeness:** When data is transformed through a pipeline (raw → mutated → applied), verify every stage connects to the next.
   - Example bug pattern: Mutation service mutates `MaxCharge` on template, but `CareerAbility` reads `MaxCharge` from career definition (unmutated source).
   - Check: For every `property="X"` in mutation XML, trace X through the mutation service to where the mutated value is consumed.

4. **Parallel Method Consistency:** When multiple methods serve the same purpose (e.g., checking cost, applying cost, displaying cost), verify they ALL use the same calculation.
   - Example bug pattern: `CanAffordUpgrade` uses `baseCost * count` while `SpendForUpgrade` uses `GetEffectiveUpgradeCost()`.
   - Check: Find method families (CanAfford/Spend/Clamp/Display) and verify they share the same cost derivation.

4b. **Engine-Float Gate NaN Polarity (MANDATORY for every decision gate on an ENGINE-sourced float):** For every comparison in the changeset that gates behavior on a float the ENGINE hands in at runtime (momentum, velocity, damage, resistance, health, distance — NOT config floats, which the FiniteFloatValidator rule covers at load), check the comparison's polarity against NaN. All NaN comparisons return false, so:
   - An inverted early-exit (`if (x <= 0f) return;` / `if (x < min || x > max) reject;`) lets NaN PASS into the active branch — flag it. The gate must be a positive requirement (`x > 0f` required to proceed / `!(x > 0f)` to reject) or an explicit `float.IsNaN` check.
   - For owned-verdict services (`bool?` fall-through patterns), a NaN input must produce `null` (defer to vanilla), never an owned true/false computed from garbage — trace what each formula emits when an input is NaN.
   - **float→int CASTS feeding an integer guard.** For every `(int)<float>` cast in or near the changed code, ask what int it produces for NaN/±Inf and whether that value defeats a downstream guard. `(int)float.NaN` and `(int)float.PositiveInfinity` are BOTH `int.MinValue` on net472/x64, and `int.MinValue - 1` underflows (unchecked) to `int.MaxValue` — so a guard like `int head = v - 1; if (head <= 0) return 0;` silently passes a poisoned value as the largest possible budget. Require finiteness AT the cast and gate the value itself, not arithmetic derived from it. **Check every float→decision path in a touched method, not only the added lines** — the 2026-07-17 instance sat two lines above a correctly-gated new one, in the same method.
   - **Why this rule exists:** 4th instance of the NaN-gate class (Career cooldown #31, EditorCacheRebuild #38, CS_Road 2026-05-13, CombatMechanics 2026-07-02 — `momentumRemaining <= 0f` passed NaN and could force SlicedThrough chains; a NaN charge velocity became an owned `false` suppressing vanilla knockdowns). The first three were CONFIG floats and produced the loader-side rule; this instance proved the ENGINE-input side had no rule and no agent prompt asking the question. See `.claude/rules/csharp-architecture.md` "Engine-Float Decision Gates" + `docs/reviews/rca-combat-mechanics-2026-07-02.md`.

5. **Lifecycle Completeness (State Matrix):** For every "set" operation, verify there is a corresponding "clear" for ALL entity lifecycle states.
   - Entity states to check: alive, killed, unconscious, removed, mission-end, screen-close, session-end.
   - Example bug pattern: `CareerAbilityBuffTracker.SetBuff()` on activation, but `ClearBuff()` only on timeout — not on hero death.
   - Check: For every static dictionary, cached field, or session-scoped state, trace all paths that clear it.

5b. **Observation State Machines (BOUNDARY ENUMERATION):** For every static field that participates in polling or change-detection of EXTERNAL state (engine counts, file sizes, network responses, MBObjectManager queries), enumerate ALL four boundary states and classify every transition between adjacent states.
   - **Why this is separate from rule 5:** Lifecycle matrix asks *"when does this entity die?"* Observation matrix asks *"what values can this poll return, in what order, and which transitions mean what?"* Both are needed for state machines driven by external polling. Rule 5 alone is insufficient (RCA: shader-precompilation initial-zero latch, 2026-05-04).
   - **Boundary states to enumerate:**
     1. **Sentinel / uninitialized** — value set by reset/init (often `-1`, `null`, `default(T)`)
     2. **First real observation** — what the poll returns BEFORE any work has happened (often `0`, `false`, empty collection)
     3. **In-progress values** — the range during normal operation
     4. **Terminal value** — the value indicating completion (often `0`, `null`, `false`)
   - **Critical: sentinel-to-first-observation collision check.** If the sentinel value (state 1) is distinguishable from the terminal value (state 4) ONLY because state 1 has a different sentinel encoding, the change-detection logic must verify it observed at least one in-progress value (state 3) before treating a return-to-terminal as completion. A separate boolean flag (`_hasObservedWork`) is the standard fix.
   - **Example bug pattern (RCA shader-precompilation):** `_lastShaderCount = -1` (sentinel) → first frame after reset, engine returns `count = 0` (first observation, but the engine hasn't started compiling yet) → patch enters "completion" branch, calls `ResetShaderBattleActive()` → patch is dead before any real work arrives.
   - **Example bug pattern (general):** A polling loop initialised to `_lastSize = -1`, polling a file size. First poll returns `0` because the file isn't created yet. Loop fires the "file shrank to zero / vanished" branch and exits. File then grows to real size; loop is gone.
   - **Check (apply for every static state field that participates in polling):**
     - Find the field's reset/init location. What value does it start at? (state 1)
     - Find the polling source. What's the lowest possible value the poll can return? (often `0`, distinct from sentinel only by sentinel encoding) — this is state 2.
     - Walk the change-detection logic for the transition state-1 → state-2. Does it incorrectly classify this as a state-3 → state-4 (completion) transition?
     - Walk the same logic for state-3 → state-4. Confirm it IS classified as completion.
     - If both transitions fire the same code path, that's a sentinel collision — flag it. The fix is a `_hasObservedWork`-style flag that distinguishes "we're past the sentinel" from "we're at the terminal."

5c. **GameModel Cross-Entity Propagation (MANDATORY when a `Taom*Model` override returns a per-entity capability/value).** A `GameModel` override that returns a per-entity value (per `MobileParty`, per `Hero`, per `Settlement`) is NOT a per-entity-isolated decision if the engine PROPAGATES that value to related entities or RECOMPUTES it per-entity across a group. A naive gate (e.g. keying only on `IsMainParty`) desyncs the group. The 5 agents' per-file + TAOM-internal-flow reviews structurally CANNOT catch this — you must open the **engine consumer** of the override result.
   - **Check (apply for every `GameModel` override method in the changeset that returns a per-entity bool/value):**
     - Decompile the engine property/method that calls `Campaign.Current.Models.<Model>.<Method>(entity)` (installed DLLs). Grep it for the value being pushed onto attached/child collections (`_attachedParties`, `BoundVillages`, family/companions) and per-entity recompute getters (a `NavigationCapability`-style getter the engine drives across the group).
     - If the value propagates or recomputes per-entity, confirm the override MIRRORS the engine's inheritance (e.g. an attached party inherits its army leader's capability). If the override ignores attached/child entities, flag as HIGH.
     - Confirm LIFECYCLE: an entity already mid-transition must retain capability to complete/exit (a party already at sea keeps naval capability to reach land regardless of toggles — gates govern only NEW transitions). A disabled/gated path that strips capability from an in-transition entity is a soft-lock — flag as MED.
     - If the override is a port of a donor model (vanilla/DLC), DIFF the donor's same method and confirm no behavioral limb was dropped when the override changed one limb.
   - **Why this rule exists:** Codex review 62 (NavalTravel #296, 2026-06-24). `TaomPartyNavigationModel.HasNavalNavigationCapability` keyed only on `IsMainParty`; the engine force-propagates `MobileParty.IsCurrentlyAtSea` down the army attachment tree (`MobileParty.cs:493-496`) and recomputes `NavigationCapability` per party (`:464-479`), so with `ApplyToAi=false` a player-led army's attached AI parties were dragged to sea with `Default`-only nav → stranded. The data-flow agent traced TAOM-internal config flow and even reasoned the ungated terrain methods "harmless," but never opened `MobileParty` to see the cross-party propagation. All 5 deep-review agents (across two passes) missed it; Codex caught it by decompiling `MobileParty`. Memory: `feedback_gamemodel_capability_engine_propagation`; RCA: `docs/reviews/rca-navaltravel-2026-06-24.md`.

5d. **Latch Closer Coverage + Toggle Gating (MANDATORY for any window/latch flag opened in one hook and closed in others — `_windowActive`, `_inflight`, static loading-window latches).** Three checks, all from one shipped changeset (tournament-exit diagnostics, 2026-07-06; RCA `docs/reviews/rca-tournament-exit-hang-2026-07-06.md`):
   - **Closer per opener path:** enumerate every code path that OPENS the latch and verify a closer exists on EACH (or the opener is gated to the paths the closers cover, e.g. `Campaign.Current != null`). An any-mission opener with campaign-only closers leaks the latch — flag as MED+.
   - **Toggles gate I/O, never state transitions:** any `if (!IsEnabled) return;` ABOVE a `_latch = false` (or equivalent state write) means a mid-window toggle-off latches the flag — flag it. Required shape: state transition first (unconditional), then the toggle gate, then logging.
   - **Outermost-gate verification:** for every method whose state transition is (or was made) unconditional, grep ALL CALLERS for `IsEnabled`-style early-outs that re-condition it. Service-layer tests are structurally blind to hook-level gates — the Codex pass caught exactly this bypass one review after the service-layer fix shipped. Do not mark a toggle-gating finding fixed until every call path passes the state transition through.
   - Rule file: `.claude/rules/harmony-patches.md` "Latches & Toggle Gates"; master record: LESSONS-LEARNED "State, Lifecycle & Save" → "Diagnostics latches".

6. **Event Hook Coverage:** For behaviors that register campaign/mission events, verify all relevant events are hooked.
   - Example bug pattern: `OnAgentRemoved` emits kill charges but no hook exists for damage-dealt charges, even though most careers use `DamageDone` charge type.
   - Check: Read the behavior's RegisterEvents/constructor, cross-reference with the data it needs to provide.

7. **Sprite/Asset Reference Verification:** For every `Sprite="X"` in XML prefabs or `GetSprite("X")` in C#, trace X through `TAOMSpriteData.xml` to verify the sprite ID is registered and matches the PNG filename in `SpriteParts/`.
   - Example bug pattern: Code writes `Sprite="TAOM\\CareerSystem\\career_button_placeholder"` but `TAOMSpriteData.xml` registers it as `CareerSystem\career_button_placeholder` (no module prefix). Silent failure — sprite just doesn't render.
   - Check: Read TAOMSpriteData.xml, extract all `<Name>` entries, cross-reference every `Sprite=` attribute in changed prefab XML and every `GetSprite(` call in changed C#.

8. **Vanilla Interaction Safety:** For every UIExtenderEx `PrefabExtension` that injects into a vanilla prefab, check whether vanilla code makes assumptions about the target container's children (hardcoded indices, typed casts, count-based iteration).
   - Example bug pattern: Adding items to `SecondaryInfoItems` collection — vanilla `HandlePanelSwitchingInput` indexes by hardcoded position, causing `IndexOutOfRangeException`.
   - Example bug pattern: Appending a non-template child to a data-bound `ListPanel` — vanilla teardown may cast all children to the template type.
   - Check: For each `PrefabExtension`, identify the target widget, then search decompiled vanilla code for how that widget's children are accessed. Flag any hardcoded indexing, typed iteration, or count assumptions.

9. **Harmony Patch Category Registration (MANDATORY — fires for every `[HarmonyPatch]` class in scope).** TAOM uses category-based Harmony patching exclusively. `Harmony.PatchAll()` is NEVER called. A patch class with only `[HarmonyPatch(...)]` and no `[HarmonyPatchCategory(...)]` — OR with both attributes but no matching `_harmony.PatchCategory("CategoryName")` call in `Main/SubModule.cs` — is silently dead code (no error, no warning, the patch simply never engages at runtime). The failure mode is invisible: the feature ships, tests pass, but the Harmony patch does nothing.
   - **Check (apply for every changed file containing `[HarmonyPatch]`):**
     - Grep the changed file for `[HarmonyPatchCategory(`. If absent, flag as HIGH.
     - Grep `Main/SubModule.cs` for `_harmony.PatchCategory("<category-name>")`. If absent, flag as HIGH.
     - The category name in `[HarmonyPatchCategory]` and the string in `_harmony.PatchCategory(...)` must match exactly (case-sensitive).
     - **APPLY-TIMING sub-check (registered ≠ applied in time):** find WHICH `SubModule` lifecycle method the `_harmony.PatchCategory(...)` call lives in, and confirm it runs BEFORE the patched target can first render. TAOM's late batch (`OnGameInitializationFinished`, gated `_gameInitPatchesApplied`) fires on CAMPAIGN INIT — correct only for in-game / character-creation screens. If the patched type is rendered on a **main-menu / pre-campaign screen** (Save/Load, main menu, launcher — decompile to find the instantiator, e.g. `BasicCharacterTableau` is built only by `SaveLoadHeroTableauTextureProvider` on the cold Load Game screen), the category MUST be applied in `OnSubModuleLoad` or `OnBeforeInitialModuleScreenSetAsRoot` (process-static one-shot), NOT the late batch — else the prefix attaches too late and the guarded crash stays live. Flag as HIGH/CRITICAL. Do NOT accept "the category is registered" as sufficient.
   - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — `Patch39_BanditPartySize` had `[HarmonyPatch]` but no `[HarmonyPatchCategory]`. The apply-timing sub-check was added after issue #299 (2026-06-24): the Save/Load CTD guard reused `Patch2_RefreshTableau` (applied at campaign-init) to protect a cold-menu screen; all 5 agents passed (the Data Flow agent's init-trace conflated "after module load" with "after game-init"), Codex caught it CRITICAL. See `docs/reviews/rca-savetableau-2026-06-24.md`. The 5 deep-review agents all missed it because none of them grepped SubModule.cs for the registration. Result: the postfix scaling bandit party troop counts would have been completely dead in production. Memory: [`feedback_harmony_patch_category_registration_verification.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_harmony_patch_category_registration_verification.md).
   - **Reference template (correct pattern from `Patch38_SettlementNameplateFade`):**
     ```csharp
     [HarmonyPatch(typeof(SettlementNameplateWidget), "DetermineTargetAlphaValue")]
     [HarmonyPatchCategory("Patch38_SettlementNameplateFade")]   // ← REQUIRED
     public static class SettlementNameplateWidget_DetermineTargetAlphaValue_Patch
     ```
     ```csharp
     // In Main/SubModule.cs, alongside the other PatchCategory calls:
     _harmony.PatchCategory("Patch38_SettlementNameplateFade");   // ← REQUIRED
     ```

10. **Cross-Module XML Data Dependency (MANDATORY — fires whenever modified XML in module A references entities defined in module B, both TAOM-managed).** TAOM ships multiple modules: `TAOM` (Main), `TAOM_Map`, `TAOM.Dependencies`, alias stubs. When module A's XML attribute references an entity defined in module B (`culture="Culture.X"` where X is in B; `troop="NPCCharacter.Y"`; `default_party_template="PartyTemplate.Z"`; settlement IDs; item IDs; etc.), module A's `SubModule.xml` MUST declare `<DependedModule Id="B"/>` AND `<DependedModuleMetadata id="B" order="LoadBeforeThis"/>`. The Bannerlord launcher does NOT infer load-order from XML cross-references — it reads only the `<DependedModules>` declaration.
    - **Check (apply when ANY modified XML lives in or references a TAOM-controlled module's `ModuleData/`):**
      - For each modified XML file in module A, grep its `culture=`, `troop=`, `*_party_template=`, settlement-id, and item-id references.
      - For each reference, identify the producing module (run `grep -l '<Culture\s\+id="X"' Modules/*/ModuleData/*.xml`).
      - If the producer is a different TAOM-managed module, open `A/SubModule.xml` and verify `<DependedModule Id="<producer>"/>` is present. If absent, flag as HIGH.
    - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — 5 LOTR bandit cultures defined in TAOM Main's `taom_spcultures.xml`, then 99 hideouts in `TAOM_Map/settlements.xml` rewritten to reference them. `TAOM_Map/SubModule.xml` had no `<DependedModule Id="TAOM"/>`. Load-order was accidental. Memory: [`feedback_cross_module_data_dependency_declaration.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_cross_module_data_dependency_declaration.md).
    - **Note:** External modules (Native, SandBoxCore, Sandbox, CustomBattle, StoryMode) have stable well-known load order and don't need this check applied to them. Apply only to TAOM-controlled modules.

11. **XML Parse Smoke Test (MANDATORY — fires for every modified ModuleData XML file).** Every new or modified XML file under any TAOM-managed module's `ModuleData/` must parse cleanly via PowerShell `[xml]$x = Get-Content -Raw <file>`. XML spec edge cases that pass eyeball review but reject at parse time:
    - `--` (double-hyphen) inside an XML comment body — XML spec forbids; engine rejects file.
    - Unescaped `&`, `<`, `>` in attribute values.
    - Mismatched element tags (`<Foo>...</Bar>`).
    - Duplicate attribute on same element.
    - Stray BOM in non-root position from copy-paste from concatenated files.
    - **Check (apply for every modified file in changeset matching `*.xml` under `ModuleData/`):**
      ```bash
      pwsh -Command '[xml]$x = Get-Content -Raw "<file path>"; "<file>: OK"'
      ```
      If any modified XML throws at parse, flag as CRITICAL — engine WILL reject the file at load.
    - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — `taom_partyTemplates.xml` had `--` inside a comment body. 5 deep-review Claude agents read the XML semantically but none parsed it. Engine would have rejected the file silently at load. Memory: [`feedback_xml_parser_smoke_test_before_commit.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_xml_parser_smoke_test_before_commit.md).

12. **Bandit Culture / Clan Pair Coverage (fires when any `<Culture is_bandit="true">` row is added in the changeset).** A `Culture.is_bandit="true"` row alone does NOT create a bandit clan in vanilla — `Hideout.MapFaction` resolves via `clan.IsBanditFaction`, which is loaded from `<Faction is_bandit="true">` rows in `spclans`/`taom_spclans`/`characters/clans.xml`. Every new bandit culture needs a matching bandit clan row.
    - **Check (apply when any added/modified culture has `is_bandit="true"`):**
      - For each such culture, grep `Main/_Module/ModuleData/characters/clans.xml` (and any other `spclans*.xml` in scope) for a `<Faction>` row where `is_bandit="true"` AND `culture="Culture.<the-new-culture>"`.
      - If absent, flag as HIGH. Hideouts referencing the culture will have unresolvable `MapFaction`, and `BanditSpawnCampaignBehavior` may NRE on spawn.
      - The bandit clan row must also specify `initial_home_settlement` pointing at a real settlement of the right type (typically a hideout), and `default_party_template` pointing at a real party template.
    - **Why this rule exists:** Bandit Management Codex review (2026-05-27) — 5 new bandit cultures authored without matching clan rows. The reasoning at design time was "the engine will auto-create bandit clans from `is_bandit` cultures" — vanilla does not do this. Memory cross-link: [`feedback_classify_by_grep_not_by_assumption.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_classify_by_grep_not_by_assumption.md) (sibling pattern).

OUTPUT FORMAT:
For each trace:
- DATA FLOW: [source] → [transform] → [consumer]
- STATUS: ✅ CONNECTED / ❌ GAP FOUND / ⚠️ INCONSISTENT
- If GAP/INCONSISTENT: describe exactly what's missing and which files are involved

Summary: N flows traced, X gaps found, Y inconsistencies found
```

## Step 2b: Adversarial Escalation (conditional)

**Only launch this step if Agent 1 (Standards) reports ANY violation rated CRITICAL.**

A CRITICAL violation is any of:
- Direct TaleWorlds sealed type usage in a service class (ADR-007 breach)
- Harmony patch that directly accesses game state without an adapter
- Entry point over 150 lines that does business logic itself

If triggered, launch a 6th agent targeting ONLY the offending files:

```
subagent_type: Explore
model: sonnet
```

**Prompt:**
```
ADVERSARIAL REVIEW — assume this code has a critical architecture violation. Prove it.

FILES WITH REPORTED VIOLATIONS: [list only the files Agent 1 flagged as CRITICAL]

For each file:
1. Read the ENTIRE file — not just the flagged lines
2. Map every dependency: what does this class hold references to? What does it return?
3. Find the blast radius: if this adapter pattern violation is kept, which other classes are contaminated?
4. Identify the minimum surgical fix: what is the smallest change that restores compliance without a rewrite?
5. Check if there is a corresponding test that would CATCH this violation (an integration test that passes a real TaleWorlds type). If not, that's a second finding.

OUTPUT FORMAT:
CONFIRMED / DISPUTED for each violation:
- CONFIRMED: [file:line] [exact violation] — blast radius: [N classes affected] — minimum fix: [description]
- DISPUTED: [file:line] [why Agent 1 was wrong]

Minimum fix plan (in order of least disruption):
1. ...
```

## Step 2c: Adaptive Expansion (always evaluate)

After the core 5 agents complete, assess whether the findings warrant additional focused agents. There is NO upper limit.

**Launch additional agents when:**
- Agent 5 (Data Flow) finds gaps → launch per-gap investigation agents to trace the full chain and propose fixes
- Multiple XML config files changed → launch one agent per config file to cross-reference all consumers
- Multiple Harmony patches changed → launch one agent per patch to verify target method signatures and side effects
- Multiple GameModel overrides changed → launch one agent to verify all overrides are registered and don't conflict
- Any agent reports >3 issues → launch a focused agent on just those files to determine root cause
- Feature spans >3 features/ subdirectories → launch per-feature agents with full context of that feature
- **The changeset includes new/modified `tools/**/*.py` or `*.ps1` that WRITE files (especially outside the repo, e.g. the external `TAOM_Map` game-install) → launch a TOOLING CORRECTNESS agent (`Explore`, `sonnet`).** The 5 core agents are C#-centric and do NOT review script tooling; a data-mutating script bug can corrupt live data silently. The agent must check: (1) **encoding/BOM preservation** — detect via `read_bytes().startswith(b"\xef\xbb\xbf")`, decode `utf-8-sig`, write `write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))`; NEVER a U+FEFF string literal, NEVER plain `utf-8` read on a BOM file; preserve CRLF + non-ASCII (`tools/README.md` "XML I/O convention"); (2) **idempotency** — re-running doesn't double-apply/corrupt; (3) **dry-run vs apply gating** — no write on `--dry-run`; (4) **backup-before-write** for destructive/external edits; (5) **regex over/under-match** — attribute targeting can't partial-match a larger token; (6) **case-insensitivity** on scene/asset/id comparisons (Windows lookup is); (7) **match-count reporting** so a human can sanity-check. Why: deep-review 2026-05-28 (scene tooling) — the BOM-handling inconsistency across the new script family was invisible to all 5 core agents; caught only by a dedicated tooling agent. RCA: `docs/reviews/rca-scene-tooling-2026-05-28.md`. Memory: `feedback_xml_tool_bom_io_convention.md`.

**Launch additional Codex passes when:**
- Any Claude agent and Codex disagree → dispatch a second Codex pass focused on the disputed finding
- Data Flow agent finds a gap Codex missed → dispatch Codex with the specific gap description to get independent verification

**The review is done when:** All agents have reported, all disagreements are resolved, and no agent's findings suggest an unexplored area.

## Step 3: Compile Report

After all 5 agents complete, compile their results into a single report:

```
DEEP REVIEW REPORT
===================
Feature: [name or "uncommitted changes"]
Date: [today]

STANDARDS:     [PASS/FAIL — N violations]
COMPATIBILITY: [PASS/FAIL — N incompatible, N unverified]
EFFICIENCY:    [PASS/FAIL — N issues (H high, M medium, L low)]
COMPLETENESS:  [COMPLETE/INCOMPLETE — list missing items]
DATA FLOW:     [PASS/FAIL — N gaps, N inconsistencies]

─────────────────────────
DETAILS
─────────────────────────

[Agent 1 results — Standards]

[Agent 2 results — Compatibility]

[Agent 3 results — Efficiency]

[Agent 4 results — Completeness]

[Agent 5 results — Data Flow]

─────────────────────────
ACTION ITEMS
─────────────────────────
1. [Most critical issue first]
2. ...

VERDICT: READY FOR COMMIT / NEEDS FIXES
```

## Step 3e: Root Cause Analysis (MANDATORY — BLOCKING GATE before commit)

If any agent (or Codex pre-review, if Step 0 ran) returned ANY confirmed finding (any severity, including LOW), Phase 3e RCA applies before the closing commit. Per `.claude/rules/harness-facts.md` and `feedback_root_cause_mandatory.md` — this is not optional, not severity-gated, not "only HIGH." The literal text: *"Do NOT skip this step. The point is not just to fix bugs — it's to make the same category of bug impossible in future features."*

The recurring failure is conflating severity with importance for RCA: we patch LOW symptoms but never extract the systemic lesson, and the same category of bug ships again. Three examples on file: Career cooldown review #31 (NaN gate), EditorCacheRebuild review #38 (NaN gate again), scene-scripts CS_Road 2026-05-13 (NaN gate, THIRD time). All caught by the same rule that was scope-gapped on each project.

**For EVERY confirmed finding (not just HIGH/MED):**

**First, confirm it is actually confirmed.** Per `.claude/rules/evidence-over-claims.md`, a finding is "confirmed" only if you (or the agent) re-read the actual TAOM source / decompiled vanilla and verified the bug exists — not because an agent reported it confidently. If you took an agent's finding on faith, re-read the code now; an unverified finding is re-flagged for investigation, not entered into RCA.

1. Write the finding text + severity.
2. **Why missed:** what assumption, scope gap, or pattern blindness let it through? Be specific — name the rule that should have caught it, name the file/line that exhibits the pattern.
3. **Preventive action:** generalizable rule, feedback-memory entry, or scope extension to an existing rule? Or one-off?
4. If the pattern has shipped before (grep `docs/reviews/rca-*.md` + `~/.claude/projects/.../memory/feedback_*.md` for it), call that out — repeat-offender bugs need stronger preventive action than first-time bugs.

**Write the result to `docs/reviews/rca-<feature>-<YYYY-MM-DD>.md`** following the format of `docs/reviews/rca-quickactions-2026-05-06.md` or `docs/reviews/rca-scene-scripts-cs-road-2026-05-13.md`:
- Top-line summary
- Findings table: # | Sev | Bug | Category | Why Missed | Preventive Action
- Root-cause pattern section (if 2+ findings share a theme)
- "Why each agent missed these" section — for each of the 5 deep-review agents that didn't catch the finding, state why their rule set didn't apply or why the agent's scope was too narrow
- "Feedback memories to codify" section — only if there's a genuine systemic pattern; don't manufacture rules

**Append the durable lesson to the master record.** For each systemic finding, also add an entry to the matching category file under `docs/reviews/lessons/` (e.g. `lessons/gamemodels-services.md`, `lessons/harmony-il.md`; index: `docs/reviews/LESSONS-LEARNED.md`), in the house shape (`### <imperative rule>` → `**Why missed:**` → `**Prevent:**` → `**Source:**`). The per-feature `rca-*.md` is the incident report; the lessons entry is the cross-feature rule that stops the *category* from recurring — it is the canonical, always-consulted record (indexed from the harness `MEMORY.md`). Read the relevant category FILE before the next review of that subsystem (per-category files keep the read cheap). Only touch `MEMORY.md` if a new feature/topic memory file is involved.

**This file MUST exist BEFORE the closing commit.** The commit message should reference the RCA path.

If the RCA reveals a rule that's already documented but wasn't followed (scope gap or agent prompt missing the rule), update the rule file or agent prompt in a follow-up commit. Commit graph: review → fixes → RCA → preventive-rule update.

## Important

- This is a READ-ONLY review. Do NOT make any code changes.
- If any agent fails to launch (MCP issues, etc.), note it in the report and run the checks manually.
- The Bannerlord compatibility agent (Agent 2) MUST use installed DLLs via ilspycmd, NOT the decompiled folder at E:\Decompiled_Bannerlord\ (it's a different version).
- Agent 5 (Data Flow) is the highest-value agent — it catches the class of bugs that all other agents consistently miss. Every HIGH bug found by Codex in this project was a data flow gap.
- If the verdict is NEEDS FIXES, list the fixes needed in priority order.

## HIGH findings — no silent deferrals (MANDATORY)

If any agent reports a HIGH-severity finding (or Codex reports P1):
1. The default action is FIX. Implement the fix in the same session.
2. If the user explicitly chooses to defer, the deferral MUST be recorded in one of:
   - A GitHub issue (`gh issue create`) with the finding text
   - A commit trailer `Deferred: <reason>` on the commit that would have fixed it
   - A CHANGELOG "Known limitation:" bullet

What is NOT allowed: quietly proceeding past a HIGH finding on informal reasoning ("only matters in case X") without writing the decision down. Past experience: Career System P2 (ally buff overwrite) was flagged HIGH by Agent 5 and dismissed — Codex independently caught the same bug later. Memory: `feedback_dont_defer_high_review_findings.md`.

## Fix-loop guidance

When the verdict is NEEDS FIXES and the user chooses to address findings now:

- If fixes are confined to one feature module, **suggest `/freeze`** scoped to that module before starting. Prevents the fix-loop from drifting into adjacent code that wasn't part of the review.
- If a finding is structural (root cause unclear, multiple symptoms in one area), invoke **`/investigate`** instead of fixing directly — its 6-phase workflow auto-engages `/freeze` and enforces the Iron Law.
- **Verify each fix at the OUTERMOST gate, not just the layer you edited.** When a fix makes a method's behavior unconditional (or changes its guard semantics), grep every CALLER of that method for early-outs that re-condition it before marking the finding fixed — and note that layer-local regression tests cannot see caller-level gates. (Codex review 72 caught a service-layer "unconditional close" fix bypassed by two hook-level `IsEnabled` gates one review after it shipped; RCA `rca-tournament-exit-hang-2026-07-06.md` finding #4.)
- After fixes land, re-run `/deep-review` (or `/deep-review --codex`) on the changed scope to confirm no new HIGH findings introduced.
