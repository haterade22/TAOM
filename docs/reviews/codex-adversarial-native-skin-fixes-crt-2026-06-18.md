OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019edc4d-4cb3-7bd0-ac39-79d4ef61877f
--------
user
# Codex Adversarial Review -- NativeSkinFixes CRT load-failure fix (Win32 error 126)

You are an adversarial reviewer. Assume there is a bug and prove it. This is a BUILD-INFRASTRUCTURE + TOOLING change, NOT a gameplay feature -- there are no Harmony patches, GameModels, or culture/kingdom IDs to cross-reference here. Focus entirely on the correctness of the build config, the three CRT guards, the bash hook, and the small C# diagnostic change.

## Background (root cause already proven, do NOT re-litigate it)

A player reported `TAOM.NativeSkinFixes.dll failed to load -- feature inert. LoadLibrary failed (Win32 error 126)`. Win32 126 = ERROR_MOD_NOT_FOUND (a module in the dependency chain is missing). Running `python tools/pe_inspect.py` on the vendored DLL proved it imported `MSVCP140D.dll`, `VCRUNTIME140D.dll`, `VCRUNTIME140_1D.dll`, `ucrtbased.dll` -- the DEBUG CRT, which is NOT redistributable (ships only with Visual Studio). The DLL was a Debug build, and the vcxproj Debug config had no `<RuntimeLibrary>` element, so MSBuild defaulted it to `/MDd` (dynamic debug CRT). MinHook.x64.dll is NOT the culprit -- it imports only KERNEL32.dll.

TAOM ships the DEBUG build (the maintainer builds Debug in Visual Studio). So the fix makes Debug self-contained: the vcxproj Debug config now sets `<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>` (`/MTd`, static debug CRT). The DLL was rebuilt + re-vendored; pe_inspect confirms it now imports only `MinHook.x64.dll, KERNEL32.dll, SHELL32.dll, ole32.dll` (the last two are OS-guaranteed Windows DLLs the static CRT pulls in). Three guards prevent the regression: a post-build check in Build.ps1, a pre-commit hook, and a CI step. The CRT-detection classifier (used in all three) is the regex `VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt` (case-insensitive).

## READ FIRST

- `docs/features/native-skin-fixes.md` -- the "Build & CRT requirement (static CRT is mandatory)" section
- `tools/pe_inspect.py` -- the PE import-table inspector the guards rely on (pure stdlib)

## Files changed (review these)

1. `Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj` -- Debug `<ClCompile>` now sets `<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>` (/MTd). Release already had `MultiThreaded` (/MT).
2. `Dependencies/NativeSkinFixes.NativeHooks/Build.ps1` -- post-build guard: runs pe_inspect on the output, throws if imports match the CRT regex. `#requires -Version 5.1`.
3. `.claude/hooks/check-native-dll-crt.sh` -- NEW PreToolUse(Bash) hook: blocks `git commit` when the staged vendored DLL imports a dynamic CRT. Modeled on `.claude/hooks/check-moduledata-validation.sh`.
4. `.claude/settings.json` -- registers the hook in PreToolUse->Bash.
5. `.github/workflows/build.yml` -- new "Validate native DLL links a static CRT" step in the `validate-xml` job (ubuntu-latest).
6. `Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs` -- error-126 message now reports whether plugin + MinHook.x64.dll are present and points at the static-CRT requirement.
7. `docs/features/native-skin-fixes.md`, `CHANGELOG.md` -- docs.
8. `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` -- re-vendored static binary.

## Known Suspects (CONFIRM or DISPUTE each, with file:line evidence)

1. SUSPECT (hook fail-open): Claim -- every internal-failure path in `check-native-dll-crt.sh` (no python, no pe_inspect.py, DLL not staged, DLL absent, empty pe_inspect output, JSON-escape failure, cd failure, grep error) emits `{}` and exits 0, so the hook can NEVER block a legitimate commit due to its own failure. DISPUTE if you can find ANY path that exits non-zero, emits malformed JSON, or blocks on internal error.

2. SUSPECT (git matcher): Claim -- the two-stage matcher correctly rejects `git commit-tree`/`commit-graph` and matches `git commit`, `git commit -m`, `git -C <path> commit`, `git -c k=v commit`. DISPUTE if there is a git invocation form that should be caught but is not, or a non-commit form that is wrongly treated as a commit.

3. SUSPECT (staged-scope): Claim -- the hook only runs pe_inspect when the EXACT path `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` is in the staged set (with `--amend` unioning HEAD's files), so it does not block unrelated commits. Also: the hook checks the on-disk DLL, not the staged blob -- is that a real gap (could a bad DLL be `git add`-ed then the on-disk file swapped before commit)? Assess whether that matters in practice.

4. SUSPECT (PowerShell 5.1): Claim -- the Build.ps1 guard uses only 5.1-safe syntax (`Get-Command -ErrorAction SilentlyContinue`, `& $py.Source`, `-match`, `Out-String`, `throw`) -- no `??`, no ternary, no `&&`/`||`, no `?.`. DISPUTE if any construct requires PowerShell 7+.

5. SUSPECT (regex completeness): Claim -- `VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt` (case-insensitive) catches every dynamic-CRT import a v143-toolset build of THIS project could emit (release /MD: VCRUNTIME140.dll, VCRUNTIME140_1.dll, MSVCP140.dll, ucrtbase.dll, api-ms-win-crt-*.dll; debug /MDd: the *D variants), AND never false-matches a static build (whose imports are MinHook.x64.dll, KERNEL32.dll, SHELL32.dll, ole32.dll) or any export name / section label in pe_inspect's output. DISPUTE with a concrete DLL name that slips through, or a concrete static-build token that false-matches.

6. SUSPECT (regex engine portability): Claim -- the SAME pattern works identically in bash `grep -iqE` (POSIX ERE) and PowerShell `-match` (.NET regex). Specifically: `[0-9]+` is valid in both (we deliberately avoided `\d`, which `grep -E` does not support). DISPUTE if `[0-9]+`, `|`, or case-insensitivity behaves differently between the two engines for this pattern.

## Required adversarial analysis

A. THE CENTRAL QUESTION: enumerate EVERY way a dynamic-CRT (player-breaking) DLL could still reach a player despite all three guards. Consider: a maintainer who edits the vcxproj back to dynamic; a hand-copied DLL committed when python is absent (hook fails open); a DLL committed on a machine where pe_inspect.py is missing; the staged-blob-vs-on-disk gap; a future toolset (v15x -> MSVCP150) -- does the regex still catch it? Rank these by likelihood and state which guard (if any) catches each.

B. pe_inspect.py output-format dependency: the guards grep pe_inspect's text output. If pe_inspect's output format changed (e.g. it stopped printing the `imports (N): ...` line, or truncated a long import list via `--max-names`), would a dynamic CRT silently pass? Check whether pe_inspect can truncate the imports list and whether the CRT DLLs could fall outside the printed window.

C. The C# change in `NativeHookLoader.cs`: confirm it cannot throw (File.Exists on computed paths), does not change control flow, and that `System.IO` is imported. Is the message accurate (does it correctly distinguish "file missing" from "CRT missing")?

D. The vcxproj change: is `MultiThreadedDebug` the correct token for /MTd static debug CRT? Is it scoped to Debug only? Does linking MinHook.x64.lib (an import library for MinHook.x64.dll) interact badly with switching OUR CRT to static? Could a static-CRT DLL loaded into the Bannerlord process (which has its own CRT) cause any runtime issue (two CRTs in one process)?

## QUALITY GATES

- Every CONFIRM/DISPUTE must cite file:line and quote the relevant line.
- Do NOT flag the byte-pattern `<PATTERN_TBD>` placeholders as a bug -- they are a known separate open follow-up; this fix only makes the DLL loadable, not active.
- Do NOT propose scope creep (e.g. rewriting the hooks, adding signing). Stick to correctness of the diff.
- If you find NO bug in a suspect, say "CONFIRMED -- no issue" with the evidence. Do not manufacture findings.

## Output

Write findings grouped by severity (HIGH/MED/LOW) with file:line and a concrete fix for each. End with a one-line verdict: SHIP or NEEDS FIXES.

ERROR: The process "33408" not found.
codex
I’ll review this as a pure build/tooling change and keep the scope to the CRT guards, vcxproj, CI, hook behavior, diagnostic message, and docs. I’m going to read the changed files with line numbers first, then trace each suspect against the exact control flow.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"RuntimeLibrary|Validate native DLL|check-native-dll-crt|TAOM.NativeSkinFixes|MSVCP|VCRUNTIME|ucrtbase|api-ms-win-crt|LoadLibrary|Win32 error|File\\.Exists|NativeHookLoader|Build & CRT\" Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 .claude/hooks/check-native-dll-crt.sh .claude/settings.json .github/workflows/build.yml Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs docs/features/native-skin-fixes.md CHANGELOG.md tools/pe_inspect.py" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 .claude/hooks/check-native-dll-crt.sh .claude/settings.json .github/workflows/build.yml Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs docs/features/native-skin-fixes.md CHANGELOG.md' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
 succeeded in 400ms:
AM .claude/hooks/check-native-dll-crt.sh
 M .claude/settings.json
 M .github/workflows/build.yml
 M CHANGELOG.md
 M CLAUDE.md
 M Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
 M Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
 M Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs
 M Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
 M Main/Features/ShaderPrecompilation/ShaderPrecompilationIoC.cs
 M Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M Main/_Module/ModuleData/taom_module_strings.xml
 M Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll
 M docs/INDEX.md
 M docs/features/native-skin-fixes.md
 M docs/features/shader-precompilation.md
?? Main/Features/SettlementFood/
?? Main/Features/ShaderPrecompilation/IShaderPrecompileCrashGuard.cs
?? Main/Features/ShaderPrecompilation/ShaderPrecompileCrashGuard.cs
?? Main/_Module/ModuleData/settlement_food/
?? TAOM.Tests/Features/SettlementFood/
?? TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompileCrashGuardTests.cs
?? docs/features/settlement-food.md
?? docs/investigations/
?? docs/reference/engine/settlement-economy-food-prosperity.md
?? docs/reviews/codex-adversarial-native-skin-fixes-crt-2026-06-18.md
?? docs/reviews/codex-adversarial-native-skin-fixes-crt-2026-06-18.prompt.md
?? docs/reviews/codex-adversarial-settlement-food-2026-06-18.md
?? docs/reviews/codex-adversarial-settlement-food-2026-06-18.prompt.md
?? docs/reviews/codex-prereview-shader-crash-guard-2026-06-18.md
?? docs/reviews/codex-prereview-shader-crash-guard-2026-06-18.prompt.md
warning: could not open directory '.codex_tmp/tmp1fft9w7e/': Permission denied
warning: could not open directory '.codex_tmp/tmp1fyfj2_j/': Permission denied
warning: could not open directory '.codex_tmp/tmp29jamgyn/': Permission denied
warning: could not open directory '.codex_tmp/tmp2owwhlqy/': Permission denied
warning: could not open directory '.codex_tmp/tmp5516wbdv/': Permission denied
warning: could not open directory '.codex_tmp/tmp78qze76y/': Permission denied
warning: could not open directory '.codex_tmp/tmp8gds9xfp/': Permission denied
warning: could not open directory '.codex_tmp/tmpbwbm4o8d/': Permission denied
warning: could not open directory '.codex_tmp/tmpc85yzrbw/': Permission denied
warning: could not open directory '.codex_tmp/tmpi3maldm8/': Permission denied
warning: could not open directory '.codex_tmp/tmpkr2gpims/': Permission denied
warning: could not open directory '.codex_tmp/tmpktgkfgay/': Permission denied
warning: could not open directory '.codex_tmp/tmplcnrd81l/': Permission denied
warning: could not open directory '.codex_tmp/tmplorpih3w/': Permission denied
warning: could not open directory '.codex_tmp/tmpm39f2s15/': Permission denied
warning: could not open directory '.codex_tmp/tmpov4yy7nx/': Permission denied
warning: could not open directory '.codex_tmp/tmpsanxk9fp/': Permission denied
warning: could not open directory '.codex_tmp/tmpwy0s04pv/': Permission denied
warning: could not open directory '.codex_tmp/tmpyqy6lg5o/': Permission denied
warning: could not open directory '.codex_tmp2/tmpulsj5ei5/': Permission denied
warning: could not open directory '.pytest_cache/': Permission denied

 succeeded in 402ms:
diff --git a/.claude/hooks/check-native-dll-crt.sh b/.claude/hooks/check-native-dll-crt.sh
index d317238e..d59fdd06 100755
--- a/.claude/hooks/check-native-dll-crt.sh
+++ b/.claude/hooks/check-native-dll-crt.sh
@@ -73,7 +73,7 @@ IMPORTS=$("$PY" tools/pe_inspect.py "$DLL" 2>/dev/null)
 # Dynamic CRT imports => the redistributable/debug runtime players lack. A
 # static-CRT build (Debug /MTd or Release /MT) imports only MinHook.x64.dll +
 # KERNEL32.dll and never matches this.
-if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP140|ucrtbase|api-ms-win-crt'; then
+if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
     echo '{}'; exit 0
 fi
 
diff --git a/.claude/settings.json b/.claude/settings.json
index d6ef4a0a..3459de46 100644
--- a/.claude/settings.json
+++ b/.claude/settings.json
@@ -49,6 +49,10 @@
           {
             "type": "command",
             "command": ".claude/hooks/check-moduledata-validation.sh"
+          },
+          {
+            "type": "command",
+            "command": ".claude/hooks/check-native-dll-crt.sh"
           }
         ]
       },
diff --git a/.github/workflows/build.yml b/.github/workflows/build.yml
index 30e64f1a..9c900839 100644
--- a/.github/workflows/build.yml
+++ b/.github/workflows/build.yml
@@ -74,6 +74,28 @@ jobs:
           if ($errors -gt 0) { exit 1 }
           Write-Host "All JSON files are well-formed."
 
+      - name: Validate native DLL links a static CRT
+        shell: bash
+        run: |
+          # The vendored TAOM.NativeSkinFixes.dll must link a STATIC CRT
+          # (Debug /MTd or Release /MT). A dynamic CRT (/MDd or /MD) imports
+          # vcruntime*/msvcp140*/ucrtbase*/api-ms-win-crt* — DLLs players
+          # without Visual Studio lack — so LoadLibrary fails with Win32
+          # error 126 and NativeSkinFixes goes inert. pe_inspect.py is pure
+          # stdlib and parses the PE import table cross-platform.
+          DLL="Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll"
+          if [ ! -f "$DLL" ]; then
+            echo "::notice::$DLL not present — skipping static-CRT check."
+            exit 0
+          fi
+          IMPORTS=$(python3 tools/pe_inspect.py "$DLL")
+          echo "$IMPORTS"
+          if echo "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
+            echo "::error::$DLL links a DYNAMIC CRT (imports above). Players without Visual Studio get LoadLibrary error 126. Rebuild with a static CRT — Debug /MTd or Release /MT — via Dependencies/NativeSkinFixes.NativeHooks/Build.ps1."
+            exit 1
+          fi
+          echo "Native DLL links a static CRT — no redistributable dependency."
+
   build:
     name: Build & Test
     runs-on: windows-latest
diff --git a/CHANGELOG.md b/CHANGELOG.md
index 8bad7cae..0277dff9 100644
--- a/CHANGELOG.md
+++ b/CHANGELOG.md
@@ -2,7 +2,95 @@
 
 ## 2026-06-18
 
-### chore(logging): gate ArmyTargeting + TroopWeight per-tick diagnostics to fire once
+### feat(settlement-food): fix garrison food starvation + tunable settlement food (TaomSettlementFoodModel)
+
+Settlements ran chronic food deficits. Root cause: the Troop Weight feature (`Patch17_TroopWeight`)
+postfixes the global `PartyBase.NumberOfAllMembers` getter and raises it to a *weighted* count, and
+vanilla `DefaultSettlementFoodModel` reads exactly that getter for the garrison food term
+(`NumberOfAllMembers / 20`). Elite garrisons (troop weights 2.0–3.0) therefore consumed 2–3× the food
+the engine intends — a globally-weighted getter leaking into an unrelated gameplay consumer (the food
+model), the same bug-class as the earlier phantom-wounded UI leak.
+
+New `TaomSettlementFoodModel : DefaultSettlementFoodModel` (food-model-only fix — the global getter
+stays weighted, so AI strength reads and garrison capacity are unchanged):
+
+- **Garrison correction:** since vanilla `NumberOfAllMembers == MemberRoster.TotalManCount`, the model
+  adds back `(weighted − raw) / garrisonDivisor` so the garrison term uses the raw body count. No-op
+  when Troop Weight is off (weighted == raw). Applies under siege too (the inflation is version-agnostic).
+- **Tunable knobs** (`settlement_food/settlement_food_config.json`, ships at vanilla values): garrison
+  + prosperity consumption divisors, town/castle base food, per-village multiplier, flat bonus, storage
+  caps. Production knobs are siege-gated (vanilla zeroes production under siege). Validated by
+  `SettlementFoodConfigProvider` (divisors ≥ 1; floats finite ≥ 0 via `FiniteFloatValidator`; invalid →
+  vanilla default + warning). MCM "Settlement Food → Enable Settlement Food Tuning" (on by default;
+  off = vanilla engine math).
+
+Thin model → pure `SettlementFoodService` (delta math, 27 unit tests) → `TownFoodSnapshot` boundary
+(ADR-002/007). Deep review: PASS (standards, 12/12 API compat, data-flow clean — 0 gaps). Reference
+doc `docs/reference/engine/settlement-economy-food-prosperity.md`; feature doc `docs/features/settlement-food.md`.
+
+### fix(native-skin-fixes): ship a static-CRT DLL so it loads for players (Win32 error 126)
+
+A player's log showed `TAOM.NativeSkinFixes.dll failed to load — feature inert. LoadLibrary failed
+(Win32 error 126)`. Error 126 (`ERROR_MOD_NOT_FOUND`) means a module in the DLL's dependency chain is
+missing. `python tools/pe_inspect.py` on the vendored DLL proved the cause: it imported `MSVCP140D.dll`,
+`VCRUNTIME140D.dll`, `VCRUNTIME140_1D.dll`, `ucrtbased.dll` — the **debug CRT**, which is not
+redistributable and ships only with Visual Studio. The DLL was a Debug build, and the vcxproj Debug
+config had no `<RuntimeLibrary>` element, so MSBuild defaulted it to `/MDd` (dynamic debug CRT). Every
+player without VS hit 126; installing the VC++ redist would not have helped (it carries the release CRT,
+not the debug CRT). `MinHook.x64.dll` was never the culprit — it imports only `KERNEL32.dll`.
+
+TAOM ships the Debug build, so the fix makes Debug self-contained: the vcxproj Debug config now sets
+`<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>` (`/MTd`, static debug CRT) to match Release's `/MT`.
+The DLL was rebuilt and re-vendored; `pe_inspect` confirms it now imports only `MinHook.x64.dll`,
+`KERNEL32.dll`, and the OS-guaranteed `SHELL32.dll`/`ole32.dll` — no redistributable dependency. Three
+guards prevent the regression from recurring: `Build.ps1` runs `pe_inspect` on its own output and throws
+on any dynamic-CRT import; the new `check-native-dll-crt.sh` PreToolUse hook blocks a commit that stages a
+dynamic-CRT DLL; the `validate-xml` CI job re-checks the committed binary. The C# loader's error-126
+message now reports whether the plugin + `MinHook.x64.dll` are present and points at the static-CRT
+requirement. Documented in `docs/features/native-skin-fixes.md` "Build & CRT requirement". The byte-pattern
+signatures remain `<PATTERN_TBD>` placeholders (separate open follow-up) — this fix makes the DLL loadable,
+a prerequisite for the hooks doing anything once patterns are authored. NativeSkinFixes tests 10/10 green;
+the 9 failing `VolunteerRecruitmentServiceTests` (Dol Guldur) are pre-existing and unrelated.
+
+### feat(shader-precompilation): auto-skip scenes that hard-crash the walk (per-scene crash guard)
+
+A user's mods-removed run crashed the walk at item 9 (`taom_rohan_battle_fords_of_isen_forceatmo`) with a
+pure-native ACCESS_VIOLATION during the scene's `MissionInitialize`, concurrent with the `pbr_terrain`
+input-layout-9 shader compile (the same shader that division-by-zeros at Helm's Deep) — GPU/driver-specific
+(the scene loads fine on other machines). A native scene-load crash isn't catchable in managed code, so it
+hard-stops the walk; without a guard, an affected user can never get past that item. (Diagnosed from the
+user's `taom_debug` + `rgl_log` + `palantir` triple, reconciled by timestamp; the popup-spam in their first
+report was a *pre-existing* third-party MBSuperSpeed `get_InputManager` AV, not TAOM.)
+
+New `ShaderPrecompileCrashGuard` (mirrors `BattleLoadStallMarker`): the runner writes the scene id it is
+about to load to `Logs/shader-precompile-inflight.marker` (survives a hard crash); if that marker is still
+there at the next walk's start, the scene crashed mid-load and is recorded to a persistent
+`Logs/shader-precompile-crashed-scenes.txt` skip list, which the runner drops from the plan so the walk
+completes. ScenePass items only (the character battle is essential); the marker is cleared only at full
+item resolution (load + compile + teardown), so a slow item or a clean exit never gets recorded — only a
+true process crash does. Delete the crashed-scenes file to retry. 10 new unit tests (34 total); build 0/0.
+**Known limitation:** the underlying `pbr_terrain` input-layout-9 div-by-zero (Helm's Deep + fords_of_isen)
+is a vanilla engine shader bug; the root fix (a shader-source override) is deferred — the guard makes the
+walk robust to it (and any other GPU-specific bad scene) in the meantime. Reviewed via `/deep-review --codex`
+— 5 agents + Codex `gpt-5.5 xhigh` both clean (Codex SHIP 0/0/0/0, all 7 Known Suspects DISPUTED with
+file:line evidence; the load-bearing false-skip trace confirmed every non-crash item-end clears the marker
+via the `TickEnding` resolution block). The data-flow agent caught one LOW Codex missed: `OnItemFailed`
+only checked `generation`, not `_state`, so a late failure callback could re-enter `BeginEnd` and reset the
+Ending timer — fixed by mirroring `OnItemRendering`'s `generation + _state==Starting` stale-callback guard.
+
+### fix(shader-precompilation): suppress the battle-load stall watchdog during the walk
+
+A player's cold-cache run compiled item 1 (the all-troops character battle, 3000 troops) for **830s**;
+the `BattleLoadStallWatchdog` (300s threshold) false-positived at 305s and emitted a spurious
+`[CrashReport]` bundle mid-walk. The load wasn't stuck — it finished at 830s and the walk completed —
+but the user got an alarming "crash" artifact. This hits every first-time cold-cache run, since item 1
+is always the longest. Fix: `ShaderPrecompileRunner` sets `BattleLoadStallWatchdog.SuppressStallDetection`
+(new `volatile` static) true for the whole walk (in `Begin`) and clears it in `Finish`; the watchdog's
+`Poll` early-returns while suppressed. The precompile's long loads are intentional and the runner has its
+own per-item timeouts, so the stall watchdog (which exists to catch *real* battle hangs) should stay
+quiet during the walk. Diagnosed from a player's `rgl_log` + `palantir` + `taom_debug` triple
+(the popup error they reported was actually a pre-existing third-party MBSuperSpeed `get_InputManager`
+AV — 13k occurrences starting 18h before the precompile — not TAOM). Build 0/0.
 
 Both features are confirmed working, so their per-event diagnostics (`ArmyTargeting:` border-floor /
 strength / target / distance-compensation DEBUG lines; `[TroopWeight][diag] Shed` INFO line) no longer
diff --git a/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 b/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
index a77cfce0..20ef95a0 100644
--- a/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
+++ b/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
@@ -55,6 +55,26 @@ $dllPath = Join-Path $outDir 'TAOM.NativeSkinFixes.dll'
 if (-not (Test-Path $dllPath)) {
     throw "Build succeeded but output DLL missing: $dllPath"
 }
+
+# Static-CRT guard. The shipped DLL must link the CRT statically (Debug=/MTd,
+# Release=/MT). A dynamic CRT (/MDd or /MD) imports vcruntime*/msvcp140*/
+# ucrtbase*/api-ms-win-crt* — DLLs players without Visual Studio lack — and
+# LoadLibrary then fails with error 126. Reject the bad binary before it can be
+# vendored. See docs/features/native-skin-fixes.md "Build & CRT requirement".
+$peInspect = Join-Path $scriptDir '..\..\tools\pe_inspect.py'
+$py = Get-Command python3 -ErrorAction SilentlyContinue
+if (-not $py) { $py = Get-Command python -ErrorAction SilentlyContinue }
+if ($py -and (Test-Path $peInspect)) {
+    $imports = & $py.Source $peInspect $dllPath 2>&1 | Out-String
+    if ($imports -match '(?i)VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt') {
+        Write-Host $imports
+        throw "[NativeSkinFixes] BUILD REJECTED: $dllPath links a DYNAMIC CRT (see imports above). Rebuild with a static CRT (Debug=/MTd, Release=/MT). The dynamic/debug CRT is absent on players' machines -> LoadLibrary error 126."
+    }
+    Write-Host "[NativeSkinFixes] CRT check OK -> static CRT, no redistributable dependency." -ForegroundColor Green
+} else {
+    Write-Host "[NativeSkinFixes] WARNING: python or tools/pe_inspect.py not found; skipped static-CRT import check (the commit hook + CI still gate it)." -ForegroundColor Yellow
+}
+
 $size = (Get-Item $dllPath).Length
 Write-Host "[NativeSkinFixes] OK -> $dllPath ($size bytes)" -ForegroundColor Green
 Write-Host "[NativeSkinFixes] Run './build.ps1' to repackage TAOM.dll + redeploy."
diff --git a/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj b/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
index 9062ab10..805c9328 100644
--- a/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
+++ b/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
@@ -62,6 +62,12 @@
       <PrecompiledHeaderFile>pch.h</PrecompiledHeaderFile>
       <LanguageStandard>stdcpp17</LanguageStandard>
       <AdditionalIncludeDirectories>$(ProjectDir)MinHook\include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
+      <!-- Static debug CRT (/MTd): link the CRT into the DLL so it has NO external
+           ucrtbased.dll / vcruntime140d.dll dependency. Without this, MSBuild
+           defaults Debug to /MDd (dynamic debug CRT), and the debug CRT is NOT
+           redistributable — players without Visual Studio get LoadLibrary error
+           126. TAOM ships the Debug build, so this MUST be static. -->
+      <RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>
       <ExceptionHandling>Async</ExceptionHandling>
     </ClCompile>
     <Link>
diff --git a/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs b/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
index ec0e28f6..15c44160 100644
--- a/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
+++ b/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
@@ -81,7 +81,17 @@ internal static class NativeHookLoader
         if (_hooksModule == IntPtr.Zero)
         {
             int err = Marshal.GetLastWin32Error();
-            _lastLoadError = $"LoadLibrary failed (Win32 error {err}). Expected DLL at: {Path.Combine(binDir, DllName + ".dll")}";
+            string pluginPath = Path.Combine(binDir, DllName + ".dll");
+            _lastLoadError = $"LoadLibrary failed (Win32 error {err}). Expected DLL at: {pluginPath}";
+            if (err == 126) // ERROR_MOD_NOT_FOUND: the plugin OR one of its dependency DLLs is missing.
+            {
+                bool pluginPresent = File.Exists(pluginPath);
+                bool minHookPresent = File.Exists(Path.Combine(binDir, "MinHook.x64.dll"));
+                _lastLoadError += $" (error 126 = a module in the dependency chain is missing." +
+                    $" plugin present: {pluginPresent}, MinHook.x64.dll present: {minHookPresent}." +
+                    " If both are present, the plugin was likely built against a non-static CRT — a debug/dynamic" +
+                    " build needs Visual Studio's runtime DLLs that players don't have. Rebuild static: Debug /MTd or Release /MT.)";
+            }
             return false;
         }
 
diff --git a/docs/features/native-skin-fixes.md b/docs/features/native-skin-fixes.md
index 59bc634a..86ca5661 100644
--- a/docs/features/native-skin-fixes.md
+++ b/docs/features/native-skin-fixes.md
@@ -150,6 +150,50 @@ to fill them in.
   Each hook reports success / failure individually so partial degradation is
   visible.
 
+## Build & CRT requirement (static CRT is mandatory)
+
+The native DLL **must link the C runtime statically.** This is the line between
+"loads on every player's machine" and "fails with `LoadLibrary` Win32 error 126
+for anyone without Visual Studio."
+
+- **TAOM ships the Debug build** (built from Visual Studio). The vcxproj Debug
+  config therefore sets `<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>`
+  (`/MTd`, **static** debug CRT); Release sets `MultiThreaded` (`/MT`, static).
+  Either is self-contained — never vendor a dynamic-CRT build.
+- **Why it matters:** a dynamic CRT (`/MDd` debug, or `/MD` release) makes the
+  DLL import `vcruntime140*.dll` / `msvcp140*.dll` / `ucrtbase*.dll`. The
+  **debug** CRT (`*140d.dll`, `ucrtbased.dll`) is **not redistributable** — it
+  ships only with Visual Studio — so a Debug `/MDd` build loads on a dev machine
+  but errors 126 for every player. Installing the VC++ redist does NOT help: it
+  contains the *release* CRT, not the debug CRT. MSBuild's Debug default with no
+  explicit `<RuntimeLibrary>` is `/MDd` — that exact gap shipped a Debug DLL that
+  failed for players (2026-06-18).
+- **A correct static-CRT build imports only `MinHook.x64.dll` + `KERNEL32.dll`.**
+  Verify any rebuild with `python tools/pe_inspect.py
+  Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` — any
+  `VCRUNTIME*` / `MSVCP140*` / `ucrtbase*` / `api-ms-win-crt*` import means the
+  build is dynamic and must be redone.
+- **`MinHook.x64.dll` is a required sidecar.** It is dynamically linked, but its
+  own only import is `KERNEL32.dll`, so it is itself self-contained. It must sit
+  next to the plugin in `bin/Win64_Shipping_Client/`. The `.gitignore` allowlist
+  force-includes both DLLs and the vcxproj `CopyMinHookSidecar` post-build target
+  keeps it in sync.
+
+**Three automated guards enforce this** (none replaces an in-game load test, but
+they stop the regression at the source):
+
+1. `Build.ps1` runs `pe_inspect.py` on its own output and `throw`s on any
+   dynamic-CRT import before the DLL can be vendored.
+2. The `check-native-dll-crt.sh` PreToolUse hook blocks a `git commit` that
+   stages a dynamic-CRT DLL.
+3. The `validate-xml` CI job (`.github/workflows/build.yml`) re-runs the same
+   check on the committed binary.
+
+> The byte-pattern signatures still ship as `<PATTERN_TBD>` placeholders (see
+> "Open follow-ups"), so even once the DLL loads, the hooks stay inert until the
+> patterns are authored. The static-CRT fix is a **prerequisite** — it makes the
+> DLL *loadable* on player machines — not the feature's activation.
+
 ## Tests
 
 - `TAOM.Tests/Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs` — 8
warning: in the working copy of '.claude/hooks/check-native-dll-crt.sh', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Dependencies/NativeSkinFixes.NativeHooks/Build.ps1', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs', LF will be replaced by CRLF the next time Git touches it

2026-06-18T19:55:29.503637Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "RuntimeLibrary|Validate native DLL|check-native-dll-crt|TAOM.N …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 1008ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "RuntimeLibrary|Validate native DLL|check-native-dll-crt|TAOM.N …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
The working tree has unrelated dirty files, so I’m keeping the review constrained to the files named in the prompt. The first diff shows the core changes; now I’m pulling current line-numbered contents so every suspect can be tied to exact lines instead of the unstaged diff only.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content .claude/hooks/check-native-dll-crt.sh | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj | ForEach-Object { $i++; if ($i -ge 1 -and $i -le 130) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs | ForEach-Object { $i++; if ($i -le 150) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 527ms:
   1: #requires -Version 5.1
   2: <#
   3: .SYNOPSIS
   4:     Builds the TAOM.NativeSkinFixes C++ project and deploys TAOM.NativeSkinFixes.dll
   5:     + MinHook.x64.dll into Main/_Module/bin/Win64_Shipping_Client/.
   6: 
   7: .DESCRIPTION
   8:     This is a manual, developer-side build. The TAOM dotnet build (./build.ps1)
   9:     does NOT trigger this — keeping the MSVC dependency off the critical path so
  10:     teammates / CI without C++ tooling can still build TAOM.dll.
  11: 
  12:     After running this script, run ./build.ps1 to rebuild TAOM.dll if you've made
  13:     matching C# changes. The native DLL is deployed automatically by
  14:     Bannerlord.BuildResources as part of the next dotnet build.
  15: 
  16: .PARAMETER Configuration
  17:     Debug or Release. Default: Release.
  18: 
  19: .EXAMPLE
  20:     pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
  21:     pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 -Configuration Debug
  22: #>
  23: param(
  24:     [ValidateSet('Debug', 'Release')]
  25:     [string]$Configuration = 'Release'
  26: )
  27: 
  28: $ErrorActionPreference = 'Stop'
  29: $scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
  30: $projectFile = Join-Path $scriptDir 'NativeSkinFixes.NativeHooks.vcxproj'
  31: $outDir      = Resolve-Path (Join-Path $scriptDir '..\..\Main\_Module\bin\Win64_Shipping_Client')
  32: 
  33: if (-not (Test-Path $projectFile)) {
  34:     throw "Project file not found: $projectFile"
  35: }
  36: 
  37: # Locate MSBuild via vswhere (ships with Visual Studio 2017+).
  38: $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
  39: if (-not (Test-Path $vswhere)) {
  40:     throw "vswhere.exe not found. Install Visual Studio 2019/2022 with the 'Desktop development with C++' workload."
  41: }
  42: 
  43: $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
  44: if (-not $msbuild) {
  45:     throw "MSBuild not found. Install Visual Studio with C++ workload + MSBuild component."
  46: }
  47: 
  48: Write-Host "[NativeSkinFixes] Building $Configuration|x64 via $msbuild" -ForegroundColor Cyan
  49: & $msbuild $projectFile /p:Configuration=$Configuration /p:Platform=x64 /m /v:minimal
  50: if ($LASTEXITCODE -ne 0) {
  51:     throw "MSBuild failed with exit code $LASTEXITCODE"
  52: }
  53: 
  54: $dllPath = Join-Path $outDir 'TAOM.NativeSkinFixes.dll'
  55: if (-not (Test-Path $dllPath)) {
  56:     throw "Build succeeded but output DLL missing: $dllPath"
  57: }
  58: 
  59: # Static-CRT guard. The shipped DLL must link the CRT statically (Debug=/MTd,
  60: # Release=/MT). A dynamic CRT (/MDd or /MD) imports vcruntime*/msvcp140*/
  61: # ucrtbase*/api-ms-win-crt* — DLLs players without Visual Studio lack — and
  62: # LoadLibrary then fails with error 126. Reject the bad binary before it can be
  63: # vendored. See docs/features/native-skin-fixes.md "Build & CRT requirement".
  64: $peInspect = Join-Path $scriptDir '..\..\tools\pe_inspect.py'
  65: $py = Get-Command python3 -ErrorAction SilentlyContinue
  66: if (-not $py) { $py = Get-Command python -ErrorAction SilentlyContinue }
  67: if ($py -and (Test-Path $peInspect)) {
  68:     $imports = & $py.Source $peInspect $dllPath 2>&1 | Out-String
  69:     if ($imports -match '(?i)VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt') {
  70:         Write-Host $imports
  71:         throw "[NativeSkinFixes] BUILD REJECTED: $dllPath links a DYNAMIC CRT (see imports above). Rebuild with a static CRT (Debug=/MTd, Release=/MT). The dynamic/debug CRT is absent on players' machines -> LoadLibrary error 126."
  72:     }
  73:     Write-Host "[NativeSkinFixes] CRT check OK -> static CRT, no redistributable dependency." -ForegroundColor Green
  74: } else {
  75:     Write-Host "[NativeSkinFixes] WARNING: python or tools/pe_inspect.py not found; skipped static-CRT import check (the commit hook + CI still gate it)." -ForegroundColor Yellow
  76: }
  77: 
  78: $size = (Get-Item $dllPath).Length
  79: Write-Host "[NativeSkinFixes] OK -> $dllPath ($size bytes)" -ForegroundColor Green
  80: Write-Host "[NativeSkinFixes] Run './build.ps1' to repackage TAOM.dll + redeploy."

 succeeded in 534ms:
   1: <?xml version="1.0" encoding="utf-8"?>
   2: <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
   3:   <ItemGroup Label="ProjectConfigurations">
   4:     <ProjectConfiguration Include="Debug|x64">
   5:       <Configuration>Debug</Configuration>
   6:       <Platform>x64</Platform>
   7:     </ProjectConfiguration>
   8:     <ProjectConfiguration Include="Release|x64">
   9:       <Configuration>Release</Configuration>
  10:       <Platform>x64</Platform>
  11:     </ProjectConfiguration>
  12:   </ItemGroup>
  13:   <PropertyGroup Label="Globals">
  14:     <VCProjectVersion>17.0</VCProjectVersion>
  15:     <Keyword>Win32Proj</Keyword>
  16:     <ProjectGuid>{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}</ProjectGuid>
  17:     <RootNamespace>TAOMNativeSkinFixes</RootNamespace>
  18:     <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
  19:   </PropertyGroup>
  20:   <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
  21:   <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" Label="Configuration">
  22:     <ConfigurationType>DynamicLibrary</ConfigurationType>
  23:     <UseDebugLibraries>true</UseDebugLibraries>
  24:     <PlatformToolset>v143</PlatformToolset>
  25:     <CharacterSet>Unicode</CharacterSet>
  26:   </PropertyGroup>
  27:   <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'" Label="Configuration">
  28:     <ConfigurationType>DynamicLibrary</ConfigurationType>
  29:     <UseDebugLibraries>false</UseDebugLibraries>
  30:     <PlatformToolset>v143</PlatformToolset>
  31:     <WholeProgramOptimization>true</WholeProgramOptimization>
  32:     <CharacterSet>Unicode</CharacterSet>
  33:   </PropertyGroup>
  34:   <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
  35:   <ImportGroup Label="ExtensionSettings">
  36:   </ImportGroup>
  37:   <ImportGroup Label="Shared">
  38:   </ImportGroup>
  39:   <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
  40:     <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
  41:   </ImportGroup>
  42:   <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
  43:     <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
  44:   </ImportGroup>
  45:   <PropertyGroup Label="UserMacros" />
  46:   <!-- Output: write TAOM.NativeSkinFixes.dll directly into Main module bin so
  47:        Bannerlord.BuildResources picks it up on the next dotnet build. The
  48:        intermediate .obj/.pch live alongside the project under obj/Config/. -->
  49:   <PropertyGroup>
  50:     <OutDir>$(ProjectDir)..\..\Main\_Module\bin\Win64_Shipping_Client\</OutDir>
  51:     <IntDir>$(ProjectDir)obj\$(Configuration)\</IntDir>
  52:     <TargetName>TAOM.NativeSkinFixes</TargetName>
  53:     <TargetExt>.dll</TargetExt>
  54:   </PropertyGroup>
  55:   <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
  56:     <ClCompile>
  57:       <WarningLevel>Level3</WarningLevel>
  58:       <SDLCheck>true</SDLCheck>
  59:       <PreprocessorDefinitions>_DEBUG;_WINDOWS;_USRDLL;%(PreprocessorDefinitions)</PreprocessorDefinitions>
  60:       <ConformanceMode>true</ConformanceMode>
  61:       <PrecompiledHeader>Use</PrecompiledHeader>
  62:       <PrecompiledHeaderFile>pch.h</PrecompiledHeaderFile>
  63:       <LanguageStandard>stdcpp17</LanguageStandard>
  64:       <AdditionalIncludeDirectories>$(ProjectDir)MinHook\include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
  65:       <!-- Static debug CRT (/MTd): link the CRT into the DLL so it has NO external
  66:            ucrtbased.dll / vcruntime140d.dll dependency. Without this, MSBuild
  67:            defaults Debug to /MDd (dynamic debug CRT), and the debug CRT is NOT
  68:            redistributable — players without Visual Studio get LoadLibrary error
  69:            126. TAOM ships the Debug build, so this MUST be static. -->
  70:       <RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>
  71:       <ExceptionHandling>Async</ExceptionHandling>
  72:     </ClCompile>
  73:     <Link>
  74:       <SubSystem>Windows</SubSystem>
  75:       <GenerateDebugInformation>true</GenerateDebugInformation>
  76:       <EnableUAC>false</EnableUAC>
  77:       <AdditionalLibraryDirectories>$(ProjectDir)MinHook\lib</AdditionalLibraryDirectories>
  78:       <AdditionalDependencies>MinHook.x64.lib;%(AdditionalDependencies)</AdditionalDependencies>
  79:     </Link>
  80:   </ItemDefinitionGroup>
  81:   <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
  82:     <ClCompile>
  83:       <WarningLevel>Level3</WarningLevel>
  84:       <FunctionLevelLinking>true</FunctionLevelLinking>
  85:       <IntrinsicFunctions>true</IntrinsicFunctions>
  86:       <SDLCheck>true</SDLCheck>
  87:       <PreprocessorDefinitions>NDEBUG;_WINDOWS;_USRDLL;%(PreprocessorDefinitions)</PreprocessorDefinitions>
  88:       <ConformanceMode>true</ConformanceMode>
  89:       <PrecompiledHeader>Use</PrecompiledHeader>
  90:       <PrecompiledHeaderFile>pch.h</PrecompiledHeaderFile>
  91:       <LanguageStandard>stdcpp17</LanguageStandard>
  92:       <AdditionalIncludeDirectories>$(ProjectDir)MinHook\include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
  93:       <RuntimeLibrary>MultiThreaded</RuntimeLibrary>
  94:       <ExceptionHandling>Async</ExceptionHandling>
  95:     </ClCompile>
  96:     <Link>
  97:       <SubSystem>Windows</SubSystem>
  98:       <EnableCOMDATFolding>true</EnableCOMDATFolding>
  99:       <OptimizeReferences>true</OptimizeReferences>
 100:       <GenerateDebugInformation>true</GenerateDebugInformation>
 101:       <EnableUAC>false</EnableUAC>
 102:       <AdditionalLibraryDirectories>$(ProjectDir)MinHook\lib</AdditionalLibraryDirectories>
 103:       <AdditionalDependencies>MinHook.x64.lib;%(AdditionalDependencies)</AdditionalDependencies>
 104:     </Link>
 105:   </ItemDefinitionGroup>
 106:   <ItemGroup>
 107:     <ClInclude Include="pch.h" />
 108:     <ClInclude Include="HairClothHook.h" />
 109:     <ClInclude Include="FaceMeshObserveHook.h" />
 110:     <ClInclude Include="CoversHeadHook.h" />
 111:     <ClInclude Include="Logging.h" />
 112:     <ClInclude Include="SignatureScanner.h" />
 113:     <ClInclude Include="Signatures.h" />
 114:   </ItemGroup>
 115:   <ItemGroup>
 116:     <ClCompile Include="pch.cpp">
 117:       <PrecompiledHeader Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">Create</PrecompiledHeader>
 118:       <PrecompiledHeader Condition="'$(Configuration)|$(Platform)'=='Release|x64'">Create</PrecompiledHeader>
 119:     </ClCompile>
 120:     <ClCompile Include="dllmain.cpp" />
 121:     <ClCompile Include="HairClothHook.cpp" />
 122:     <ClCompile Include="FaceMeshObserveHook.cpp" />
 123:     <ClCompile Include="CoversHeadHook.cpp" />
 124:     <ClCompile Include="Logging.cpp" />
 125:     <ClCompile Include="SignatureScanner.cpp" />
 126:   </ItemGroup>
 127:   <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
 128:   <ImportGroup Label="ExtensionTargets">
 129:   </ImportGroup>
 130:   <!-- Post-build: copy MinHook.x64.dll alongside the output so loading the

 succeeded in 541ms:
   1: using System;
   2: using System.IO;
   3: using System.Runtime.InteropServices;
   4: using TaleWorlds.ModuleManager;
   5: 
   6: namespace TAOM.Features.NativeSkinFixes.Interop;
   7: 
   8: /// <summary>
   9: /// Loads <c>TAOM.NativeSkinFixes.dll</c> + its MinHook dependency from the
  10: /// TAOM module's <c>bin\Win64_Shipping_Client</c> directory and exposes
  11: /// <c>GetExport&lt;T&gt;</c> for the three hook installers to resolve their
  12: /// extern "C" entry points.
  13: /// </summary>
  14: /// <remarks>
  15: /// <para>Unlike the upstream NativeSkinFixes mod, this loader does NOT resolve
  16: /// per-function RVAs in <c>TaleWorlds.Native.dll</c> — the C++ side scans for
  17: /// byte patterns at install time (see <c>Signatures.h</c>). The interop layer
  18: /// is therefore version-independent: only the C++ patterns change between
  19: /// Bannerlord releases.</para>
  20: /// <para>Idempotent: <see cref="EnsureLoaded"/> is safe to call multiple times.
  21: /// First call resolves and caches; subsequent calls return the cached state.</para>
  22: /// </remarks>
  23: internal static class NativeHookLoader
  24: {
  25:     private const string DllName = "TAOM.NativeSkinFixes";
  26: 
  27:     [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
  28:     private static extern IntPtr LoadLibrary(string lpFileName);
  29: 
  30:     [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
  31:     private static extern bool SetDllDirectory(string? lpPathName);
  32: 
  33:     [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
  34:     private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
  35: 
  36:     private static IntPtr _hooksModule;
  37:     private static bool _loadAttempted;
  38:     private static string? _lastLoadError;
  39: 
  40:     /// <summary>Handle to the loaded <c>TAOM.NativeSkinFixes.dll</c>, or <see cref="IntPtr.Zero"/>.</summary>
  41:     public static IntPtr HooksModule => _hooksModule;
  42: 
  43:     /// <summary>The last LoadLibrary error message, or null on success / not-yet-attempted.</summary>
  44:     public static string? LastLoadError => _lastLoadError;
  45: 
  46:     /// <summary>
  47:     /// Loads <c>TAOM.NativeSkinFixes.dll</c> from <c>Modules/TAOM/bin/Win64_Shipping_Client</c>.
  48:     /// Returns true on success. Subsequent calls return the cached result.
  49:     /// </summary>
  50:     public static bool EnsureLoaded()
  51:     {
  52:         if (_hooksModule != IntPtr.Zero) return true;
  53:         if (_loadAttempted) return false;
  54:         _loadAttempted = true;
  55: 
  56:         string modulePath;
  57:         try
  58:         {
  59:             modulePath = ModuleHelper.GetModuleFullPath("TAOM");
  60:         }
  61:         catch (Exception ex)
  62:         {
  63:             _lastLoadError = $"ModuleHelper.GetModuleFullPath threw: {ex.GetType().Name}: {ex.Message}";
  64:             return false;
  65:         }
  66: 
  67:         string binDir = Path.GetFullPath(Path.Combine(modulePath, "bin", "Win64_Shipping_Client"));
  68: 
  69:         // Setting the DLL directory so MinHook.x64.dll resolves alongside the
  70:         // native plugin even if the working directory differs.
  71:         SetDllDirectory(binDir);
  72:         try
  73:         {
  74:             _hooksModule = LoadLibrary(DllName + ".dll");
  75:         }
  76:         finally
  77:         {
  78:             SetDllDirectory(null);
  79:         }
  80: 
  81:         if (_hooksModule == IntPtr.Zero)
  82:         {
  83:             int err = Marshal.GetLastWin32Error();
  84:             string pluginPath = Path.Combine(binDir, DllName + ".dll");
  85:             _lastLoadError = $"LoadLibrary failed (Win32 error {err}). Expected DLL at: {pluginPath}";
  86:             if (err == 126) // ERROR_MOD_NOT_FOUND: the plugin OR one of its dependency DLLs is missing.
  87:             {
  88:                 bool pluginPresent = File.Exists(pluginPath);
  89:                 bool minHookPresent = File.Exists(Path.Combine(binDir, "MinHook.x64.dll"));
  90:                 _lastLoadError += $" (error 126 = a module in the dependency chain is missing." +
  91:                     $" plugin present: {pluginPresent}, MinHook.x64.dll present: {minHookPresent}." +
  92:                     " If both are present, the plugin was likely built against a non-static CRT — a debug/dynamic" +
  93:                     " build needs Visual Studio's runtime DLLs that players don't have. Rebuild static: Debug /MTd or Release /MT.)";
  94:             }
  95:             return false;
  96:         }
  97: 
  98:         return true;
  99:     }
 100: 
 101:     /// <summary>
 102:     /// Resolves a DLL export to a typed delegate. Returns null if the export
 103:     /// is missing — caller is responsible for the fallback (log + skip-install).
 104:     /// </summary>
 105:     public static T? GetExport<T>(string name)
 106:         where T : Delegate
 107:     {
 108:         if (_hooksModule == IntPtr.Zero) return null;
 109:         IntPtr proc = GetProcAddress(_hooksModule, name);
 110:         if (proc == IntPtr.Zero) return null;
 111:         return Marshal.GetDelegateForFunctionPointer<T>(proc);
 112:     }
 113: }

 succeeded in 552ms:
   1: #!/usr/bin/env bash
   2: # check-native-dll-crt.sh
   3: # PreToolUse(Bash) hook: when `git commit` stages the vendored native
   4: # TAOM.NativeSkinFixes.dll, run tools/pe_inspect.py and BLOCK the commit if the
   5: # DLL links a DYNAMIC C runtime (imports vcruntime*/msvcp140*/ucrtbase*/
   6: # api-ms-win-crt*). A dynamic/debug CRT is absent on players' machines without
   7: # Visual Studio, so LoadLibrary fails with Win32 error 126 and the feature goes
   8: # inert. The DLL MUST link a static CRT (Debug /MTd or Release /MT).
   9: #
  10: # Defense in depth: Build.ps1 already guards its own output, but this catches a
  11: # hand-copied or stale debug DLL that bypasses Build.ps1. See
  12: # docs/features/native-skin-fixes.md "Build & CRT requirement".
  13: #
  14: # Fail-open (per .claude/rules/harness-facts.md "TAOM hooks MUST fail open"):
  15: # no python, no pe_inspect.py, DLL not staged, DLL absent on disk, or any
  16: # internal error ALLOWS the commit. Only a confirmed dynamic-CRT import blocks.
  17: #
  18: # Returns: {} to allow, {"permissionDecision":"deny","message":"..."} to block.
  19: 
  20: set -uo pipefail
  21: 
  22: INPUT=$(cat)
  23: 
  24: # Extract the bash command from tool_input (mirrors check-moduledata-validation.sh).
  25: COMMAND=$(printf '%s' "$INPUT" | python3 -c '
  26: import sys, json
  27: try:
  28:     d = json.loads(sys.stdin.read())
  29:     print(d.get("tool_input", {}).get("command", ""))
  30: except Exception:
  31:     pass
  32: ' 2>/dev/null)
  33: 
  34: # Two-stage git-commit matcher: handle `git -C/-c ... commit`; reject
  35: # `git commit-tree` / `commit-graph`. Per .claude/rules/harness-facts.md.
  36: case "$COMMAND" in
  37:     *"git commit-"*) echo '{}'; exit 0 ;;
  38: esac
  39: case "$COMMAND" in
  40:     *"git commit"* | *"git -"*" commit"* ) ;;
  41:     *) echo '{}'; exit 0 ;;
  42: esac
  43: 
  44: cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }
  45: 
  46: DLL="Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll"
  47: 
  48: # Only run when this commit stages the vendored DLL. No blanket --amend skip
  49: # (amend is commonly "oops, add a file" -- include HEAD's files on amend).
  50: STAGED=$(git diff --cached --name-only 2>/dev/null)
  51: case "$COMMAND" in
  52:     *"--amend"*)
  53:         STAGED=$(printf '%s\n%s\n' "$STAGED" \
  54:             "$(git show HEAD --name-only --pretty=format: 2>/dev/null)" | sort -u)
  55:         ;;
  56: esac
  57: 
  58: HAS_DLL=0
  59: while IFS= read -r f; do
  60:     [[ "$f" == "$DLL" ]] && { HAS_DLL=1; break; }
  61: done <<< "$STAGED"
  62: [[ $HAS_DLL -eq 0 ]] && { echo '{}'; exit 0; }
  63: 
  64: # Fail open if we can't run the check.
  65: [[ -f "$DLL" ]] || { echo '{}'; exit 0; }
  66: PY=$(command -v python3 || command -v python || true)
  67: [[ -z "$PY" ]] && { echo '{}'; exit 0; }
  68: [[ -f tools/pe_inspect.py ]] || { echo '{}'; exit 0; }
  69: 
  70: IMPORTS=$("$PY" tools/pe_inspect.py "$DLL" 2>/dev/null)
  71: [[ -z "$IMPORTS" ]] && { echo '{}'; exit 0; }
  72: 
  73: # Dynamic CRT imports => the redistributable/debug runtime players lack. A
  74: # static-CRT build (Debug /MTd or Release /MT) imports only MinHook.x64.dll +
  75: # KERNEL32.dll and never matches this.
  76: if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
  77:     echo '{}'; exit 0
  78: fi
  79: 
  80: # Build a JSON-escaped deny message with the import list (bounded).
  81: MSG=$(printf '%s' "$IMPORTS" | python3 -c '
  82: import sys, json
  83: lines = [l for l in sys.stdin.read().splitlines() if l.strip()][-12:]
  84: print(json.dumps(
  85:     "[check-native-dll-crt] git commit BLOCKED: the vendored "
  86:     "Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll links a "
  87:     "DYNAMIC C runtime (imports below). Players without Visual Studio lack those "
  88:     "DLLs, so LoadLibrary fails with Win32 error 126 and NativeSkinFixes goes "
  89:     "inert. Rebuild with a STATIC CRT (Debug /MTd or Release /MT) via "
  90:     "pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1, then re-stage.\n\n"
  91:     + "\n".join(lines)))
  92: ' 2>/dev/null)
  93: [[ -z "$MSG" ]] && { echo '{}'; exit 0; }
  94: 
  95: printf '{"permissionDecision":"deny","message":%s}\n' "$MSG"
  96: exit 0

codex
The hook’s fail-open intent is visible, but I’m not assuming it holds until I’ve checked the non-obvious paths: JSON extraction, git command matching, grep/pipefail behavior, and the final JSON escaping path. I’m also checking `pe_inspect.py` because both the hook and CI depend on its text format.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content .claude/settings.json | ForEach-Object { $i++; if ($i -ge 35 -and $i -le 70) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content .github/workflows/build.yml | ForEach-Object { $i++; if ($i -ge 55 -and $i -le 105) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content tools/pe_inspect.py | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content docs/features/native-skin-fixes.md | ForEach-Object { $i++; if ($i -ge 145 -and $i -le 205) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 444ms:
  35:             "command": ".claude/hooks/validate-push.sh"
  36:           },
  37:           {
  38:             "type": "command",
  39:             "command": ".claude/hooks/check-changelog-changed.sh"
  40:           },
  41:           {
  42:             "type": "command",
  43:             "command": ".claude/hooks/check-claude-files-tracked.sh"
  44:           },
  45:           {
  46:             "type": "command",
  47:             "command": ".claude/hooks/block-dangerous-git.sh"
  48:           },
  49:           {
  50:             "type": "command",
  51:             "command": ".claude/hooks/check-moduledata-validation.sh"
  52:           },
  53:           {
  54:             "type": "command",
  55:             "command": ".claude/hooks/check-native-dll-crt.sh"
  56:           }
  57:         ]
  58:       },
  59:       {
  60:         "matcher": "Edit|Write",
  61:         "hooks": [
  62:           {
  63:             "type": "command",
  64:             "command": ".claude/hooks/config-protection.sh"
  65:           }
  66:         ]
  67:       },
  68:       {
  69:         "matcher": "",
  70:         "hooks": [

 succeeded in 447ms:
  55:               Write-Error "Malformed XSLT: $($_.FullName) - $_"
  56:               $errors++
  57:             }
  58:           }
  59:           if ($errors -gt 0) { exit 1 }
  60:           Write-Host "All XSLT files are well-formed."
  61: 
  62:       - name: Validate JSON
  63:         shell: pwsh
  64:         run: |
  65:           $errors = 0
  66:           Get-ChildItem -Path "Main/_Module/ModuleData" -Filter "*.json" -Recurse | ForEach-Object {
  67:             try {
  68:               Get-Content $_.FullName -Raw | ConvertFrom-Json | Out-Null
  69:             } catch {
  70:               Write-Error "Malformed JSON: $($_.FullName) - $_"
  71:               $errors++
  72:             }
  73:           }
  74:           if ($errors -gt 0) { exit 1 }
  75:           Write-Host "All JSON files are well-formed."
  76: 
  77:       - name: Validate native DLL links a static CRT
  78:         shell: bash
  79:         run: |
  80:           # The vendored TAOM.NativeSkinFixes.dll must link a STATIC CRT
  81:           # (Debug /MTd or Release /MT). A dynamic CRT (/MDd or /MD) imports
  82:           # vcruntime*/msvcp140*/ucrtbase*/api-ms-win-crt* — DLLs players
  83:           # without Visual Studio lack — so LoadLibrary fails with Win32
  84:           # error 126 and NativeSkinFixes goes inert. pe_inspect.py is pure
  85:           # stdlib and parses the PE import table cross-platform.
  86:           DLL="Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll"
  87:           if [ ! -f "$DLL" ]; then
  88:             echo "::notice::$DLL not present — skipping static-CRT check."
  89:             exit 0
  90:           fi
  91:           IMPORTS=$(python3 tools/pe_inspect.py "$DLL")
  92:           echo "$IMPORTS"
  93:           if echo "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
  94:             echo "::error::$DLL links a DYNAMIC CRT (imports above). Players without Visual Studio get LoadLibrary error 126. Rebuild with a static CRT — Debug /MTd or Release /MT — via Dependencies/NativeSkinFixes.NativeHooks/Build.ps1."
  95:             exit 1
  96:           fi
  97:           echo "Native DLL links a static CRT — no redistributable dependency."
  98: 
  99:   build:
 100:     name: Build & Test
 101:     runs-on: windows-latest
 102:     # Requires Bannerlord game DLLs — runs on self-hosted runner or when BANNERLORD_GAME_DIR is set
 103:     if: ${{ vars.BANNERLORD_GAME_DIR != '' || github.event_name == 'workflow_dispatch' }}
 104:     env:
 105:       BANNERLORD_GAME_DIR: ${{ vars.BANNERLORD_GAME_DIR }}

 succeeded in 456ms:
   1: #!/usr/bin/env python3
   2: """
   3: See into a NATIVE (non-.NET) Windows DLL/EXE without a decompiler: parse the PE
   4: export + import tables (the factual API surface + dependencies) using only stdlib.
   5: 
   6: For .NET assemblies use ilspycmd (full source). For native DLLs ilspycmd produces
   7: nothing, so this is how we document what they actually do as FACT, not a guess.
   8: 
   9: Usage:
  10:   python tools/pe_inspect.py <file.dll> [--max-names N]   # default N=60
  11: Prints: machine, PE32/PE32+, internal name, #exports + sample, imported DLLs.
  12: """
  13: import struct, sys, os
  14: 
  15: def _u(d, off, fmt): return struct.unpack_from(fmt, d, off)
  16: 
  17: def rva_to_off(rva, sections):
  18:     for vaddr, vsize, roff, rsize in sections:
  19:         if vaddr <= rva < vaddr + max(vsize, rsize):
  20:             return roff + (rva - vaddr)
  21:     return None
  22: 
  23: def cstr(d, off):
  24:     end = d.index(b'\x00', off)
  25:     return d[off:end].decode('ascii', 'replace')
  26: 
  27: def inspect(path, max_names=60):
  28:     d = open(path, 'rb').read()
  29:     if d[:2] != b'MZ':
  30:         return f"{os.path.basename(path)}: not a PE file"
  31:     e_lfanew = _u(d, 0x3C, '<I')[0]
  32:     if d[e_lfanew:e_lfanew+4] != b'PE\x00\x00':
  33:         return f"{os.path.basename(path)}: no PE signature"
  34:     coff = e_lfanew + 4
  35:     machine, nsec = _u(d, coff, '<HH')
  36:     opt_size = _u(d, coff + 16, '<H')[0]
  37:     opt = coff + 20
  38:     magic = _u(d, opt, '<H')[0]                 # 0x10b=PE32, 0x20b=PE32+
  39:     dd_off = opt + (112 if magic == 0x20b else 96)
  40:     export_rva = _u(d, dd_off, '<I')[0]
  41:     import_rva = _u(d, dd_off + 8, '<I')[0]
  42:     sec_off = opt + opt_size
  43:     sections = []
  44:     for i in range(nsec):
  45:         vsize, vaddr, rsize, roff = _u(d, sec_off + i*40 + 8, '<IIII')
  46:         sections.append((vaddr, vsize, roff, rsize))
  47:     out = [f"{os.path.basename(path)}",
  48:            f"  machine={'x64' if machine==0x8664 else hex(machine)}  {'PE32+' if magic==0x20b else 'PE32'}  sections={nsec}"]
  49:     # exports
  50:     if export_rva:
  51:         eo = rva_to_off(export_rva, sections)
  52:         name_rva = _u(d, eo + 0x0C, '<I')[0]
  53:         nfuncs = _u(d, eo + 0x14, '<I')[0]
  54:         nnames = _u(d, eo + 0x18, '<I')[0]
  55:         aon = _u(d, eo + 0x20, '<I')[0]
  56:         intern = cstr(d, rva_to_off(name_rva, sections)) if name_rva else '(none)'
  57:         names = []
  58:         ao = rva_to_off(aon, sections)
  59:         for i in range(min(nnames, max_names)):
  60:             nrva = _u(d, ao + i*4, '<I')[0]
  61:             names.append(cstr(d, rva_to_off(nrva, sections)))
  62:         out.append(f"  internal_name={intern}  exports: {nfuncs} funcs / {nnames} named")
  63:         if names:
  64:             out.append("  sample exports: " + ", ".join(names[:max_names]))
  65:     else:
  66:         out.append("  exports: NONE (no export directory)")
  67:     # imports (dependencies)
  68:     if import_rva:
  69:         io = rva_to_off(import_rva, sections)
  70:         imps = []
  71:         while io is not None:
  72:             nm_rva = _u(d, io + 0x0C, '<I')[0]
  73:             if nm_rva == 0:
  74:                 break
  75:             imps.append(cstr(d, rva_to_off(nm_rva, sections)))
  76:             io += 0x14
  77:         out.append(f"  imports ({len(imps)}): " + ", ".join(imps))
  78:     return "\n".join(out)
  79: 
  80: if __name__ == '__main__':
  81:     if len(sys.argv) < 2:
  82:         print(__doc__); sys.exit(1)
  83:     mx = 60
  84:     if '--max-names' in sys.argv:
  85:         mx = int(sys.argv[sys.argv.index('--max-names') + 1])
  86:     for p in [a for a in sys.argv[1:] if not a.startswith('--') and a.isascii() and os.path.exists(a)]:
  87:         print(inspect(p, mx)); print()

 succeeded in 450ms:
 145: 
 146: - **MinHook 1.3.4** — MIT-licensed third-party native detour library, vendored
 147:   under `Dependencies/NativeSkinFixes.NativeHooks/MinHook/`. No package manager
 148:   dependency.
 149: - **`IModLogger` (Core/Logging)** — used for boot-time install diagnostics.
 150:   Each hook reports success / failure individually so partial degradation is
 151:   visible.
 152: 
 153: ## Build & CRT requirement (static CRT is mandatory)
 154: 
 155: The native DLL **must link the C runtime statically.** This is the line between
 156: "loads on every player's machine" and "fails with `LoadLibrary` Win32 error 126
 157: for anyone without Visual Studio."
 158: 
 159: - **TAOM ships the Debug build** (built from Visual Studio). The vcxproj Debug
 160:   config therefore sets `<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>`
 161:   (`/MTd`, **static** debug CRT); Release sets `MultiThreaded` (`/MT`, static).
 162:   Either is self-contained — never vendor a dynamic-CRT build.
 163: - **Why it matters:** a dynamic CRT (`/MDd` debug, or `/MD` release) makes the
 164:   DLL import `vcruntime140*.dll` / `msvcp140*.dll` / `ucrtbase*.dll`. The
 165:   **debug** CRT (`*140d.dll`, `ucrtbased.dll`) is **not redistributable** — it
 166:   ships only with Visual Studio — so a Debug `/MDd` build loads on a dev machine
 167:   but errors 126 for every player. Installing the VC++ redist does NOT help: it
 168:   contains the *release* CRT, not the debug CRT. MSBuild's Debug default with no
 169:   explicit `<RuntimeLibrary>` is `/MDd` — that exact gap shipped a Debug DLL that
 170:   failed for players (2026-06-18).
 171: - **A correct static-CRT build imports only `MinHook.x64.dll` + `KERNEL32.dll`.**
 172:   Verify any rebuild with `python tools/pe_inspect.py
 173:   Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` — any
 174:   `VCRUNTIME*` / `MSVCP140*` / `ucrtbase*` / `api-ms-win-crt*` import means the
 175:   build is dynamic and must be redone.
 176: - **`MinHook.x64.dll` is a required sidecar.** It is dynamically linked, but its
 177:   own only import is `KERNEL32.dll`, so it is itself self-contained. It must sit
 178:   next to the plugin in `bin/Win64_Shipping_Client/`. The `.gitignore` allowlist
 179:   force-includes both DLLs and the vcxproj `CopyMinHookSidecar` post-build target
 180:   keeps it in sync.
 181: 
 182: **Three automated guards enforce this** (none replaces an in-game load test, but
 183: they stop the regression at the source):
 184: 
 185: 1. `Build.ps1` runs `pe_inspect.py` on its own output and `throw`s on any
 186:    dynamic-CRT import before the DLL can be vendored.
 187: 2. The `check-native-dll-crt.sh` PreToolUse hook blocks a `git commit` that
 188:    stages a dynamic-CRT DLL.
 189: 3. The `validate-xml` CI job (`.github/workflows/build.yml`) re-runs the same
 190:    check on the committed binary.
 191: 
 192: > The byte-pattern signatures still ship as `<PATTERN_TBD>` placeholders (see
 193: > "Open follow-ups"), so even once the DLL loads, the hooks stay inert until the
 194: > patterns are authored. The static-CRT fix is a **prerequisite** — it makes the
 195: > DLL *loadable* on player machines — not the feature's activation.
 196: 
 197: ## Tests
 198: 
 199: - `TAOM.Tests/Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs` — 8
 200:   tests covering the editor-mode skip predicate (null / empty / normal client
 201:   / editor / mixed case / false-positive guard) and the localization key
 202:   wiring (key format + non-empty default).
 203: 
 204: The native interop layer (LoadLibrary, GetProcAddress, MinHook trampoline
 205: install, byte-pattern scan against the live `TaleWorlds.Native.dll` image)

codex
`pe_inspect.py` prints the import DLL list without applying `--max-names`; that option only bounds export sample names. I’m going to verify that directly against the current vendored DLL and then test the regex behavior against the actual output.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj | ForEach-Object { $i++; if ($i -ge 127 -and $i -le 155) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content .claude/settings.json | ForEach-Object { $i++; if ($i -ge 1 -and $i -le 35) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'python tools/pe_inspect.py Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$imports = python tools/pe_inspect.py Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll; $imports; if ($imports -match '"'(?i)VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt') { 'MATCH' } else { 'NO_MATCH' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 312ms:
TAOM.NativeSkinFixes.dll
  machine=x64  PE32+  sections=12
  internal_name=TAOM.NativeSkinFixes.dll  exports: 7 funcs / 7 named
  sample exports: CoversHeadHook_Install, CoversHeadHook_Uninstall, FaceMeshObserveHook_Install, FaceMeshObserveHook_Uninstall, HairClothHook_HasBeardCloth, HairClothHook_Install, HairClothHook_Uninstall
  imports (4): MinHook.x64.dll, KERNEL32.dll, SHELL32.dll, ole32.dll


 succeeded in 327ms:
TAOM.NativeSkinFixes.dll
  machine=x64  PE32+  sections=12
  internal_name=TAOM.NativeSkinFixes.dll  exports: 7 funcs / 7 named
  sample exports: CoversHeadHook_Install, CoversHeadHook_Uninstall, FaceMeshObserveHook_Install, FaceMeshObserveHook_Uninstall, HairClothHook_HasBeardCloth, HairClothHook_Install, HairClothHook_Uninstall
  imports (4): MinHook.x64.dll, KERNEL32.dll, SHELL32.dll, ole32.dll

NO_MATCH

 succeeded in 470ms:
   1: {
   2:   "env": {
   3:     "CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS": "1"
   4:   },
   5:   "statusLine": {
   6:     "type": "command",
   7:     "command": "bash .claude/statusline.sh"
   8:   },
   9:   "hooks": {
  10:     "SessionStart": [
  11:       {
  12:         "matcher": "",
  13:         "hooks": [
  14:           {
  15:             "type": "command",
  16:             "command": ".claude/hooks/session-start.sh"
  17:           },
  18:           {
  19:             "type": "command",
  20:             "command": ".claude/hooks/detect-docs-gaps.sh"
  21:           }
  22:         ]
  23:       }
  24:     ],
  25:     "PreToolUse": [
  26:       {
  27:         "matcher": "Bash",
  28:         "hooks": [
  29:           {
  30:             "type": "command",
  31:             "command": ".claude/hooks/check-build-before-commit.sh"
  32:           },
  33:           {
  34:             "type": "command",
  35:             "command": ".claude/hooks/validate-push.sh"

 succeeded in 473ms:
 127:   <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
 128:   <ImportGroup Label="ExtensionTargets">
 129:   </ImportGroup>
 130:   <!-- Post-build: copy MinHook.x64.dll alongside the output so loading the
 131:        native plugin doesn't fail with code 126 ("dependent module missing")
 132:        on machines that don't have the file already in Win64_Shipping_Client. -->
 133:   <Target Name="CopyMinHookSidecar" AfterTargets="Build">
 134:     <Copy SourceFiles="$(ProjectDir)MinHook\lib\MinHook.x64.dll" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />
 135:   </Target>
 136: </Project>

codex
The current binary is static by the guard’s definition: `pe_inspect.py` reports imports `MinHook.x64.dll, KERNEL32.dll, SHELL32.dll, ole32.dll`, and the shared regex does not match it. I’m now focusing on adversarial bypasses rather than re-checking the proven root cause.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content .claude/hooks/check-moduledata-validation.sh | ForEach-Object { $i++; if ($i -le 120) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Dependencies/NativeSkinFixes.NativeHooks/*.vcxproj,Dependencies/NativeSkinFixes.NativeHooks/*.props,Dependencies/NativeSkinFixes.NativeHooks/*.targets -Pattern 'RuntimeLibrary|UseDebugLibraries|AdditionalDependencies|AdditionalLibraryDirectories|MinHook' -ErrorAction SilentlyContinue | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path .gitignore,Dependencies/NativeSkinFixes.NativeHooks/.gitignore -Pattern 'TAOM.NativeSkinFixes|MinHook|Win64_Shipping_Client' -ErrorAction SilentlyContinue | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main/Features/NativeSkinFixes/Interop/*.cs,TAOM.Tests/Features/NativeSkinFixes/*.cs -Pattern 'LastLoadError|LoadLibrary failed|MinHook|error 126|EnsureLoaded|System.IO' -ErrorAction SilentlyContinue | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 456ms:
   1: #!/usr/bin/env bash
   2: # check-moduledata-validation.sh
   3: # PreToolUse(Bash) hook: when `git commit` is about to run with TAOM ModuleData
   4: # XML staged, run the schema-driven validator and BLOCK the commit if it finds
   5: # ERROR-severity issues (broken Item/NPCCharacter refs, unknown cultures,
   6: # duplicate ids). Catches the underwear / dead-troop-ref / stale-culture /
   7: # duplicate-id bug classes before they ship.
   8: #
   9: # Why: tools/validate_moduledata.py consolidates the per-task ref validators
  10: # into one engine; this hook makes it run automatically on relevant commits
  11: # instead of relying on anyone remembering it. See
  12: # docs/features/moduledata-validation.md and .claude/rules/moduledata-validation.md.
  13: #
  14: # Scope: only ERROR-severity codes block (WARNINGs -- INVALID_ENUM,
  15: # MISSING_CIVILIAN_TYPE, BROKEN_PARTY_TEMPLATE_REF, DUPLICATE_ITEM_DEF -- do
  16: # not). Run `python tools/validate_moduledata.py` manually to see warnings.
  17: #
  18: # Fail-open (per .claude/rules/harness-facts.md "TAOM hooks MUST fail open"):
  19: # ANY hook-internal failure -- no python, validator crash, missing game install
  20: # (rc=2), nothing staged -- ALLOWS the commit. Only a genuine validator ERROR
  21: # exit (rc=1) blocks.
  22: #
  23: # Returns: {} to allow, {"permissionDecision":"deny","message":"..."} to block.
  24: 
  25: set -uo pipefail
  26: 
  27: INPUT=$(cat)
  28: 
  29: # Extract the bash command from tool_input (mirrors check-changelog-changed.sh).
  30: COMMAND=$(printf '%s' "$INPUT" | python3 -c '
  31: import sys, json
  32: try:
  33:     d = json.loads(sys.stdin.read())
  34:     print(d.get("tool_input", {}).get("command", ""))
  35: except Exception:
  36:     pass
  37: ' 2>/dev/null)
  38: 
  39: # Two-stage git-commit matcher: handle `git -C/-c ... commit`; reject
  40: # `git commit-tree` / `commit-graph`. Per .claude/rules/harness-facts.md.
  41: case "$COMMAND" in
  42:     *"git commit-"*) echo '{}'; exit 0 ;;
  43: esac
  44: case "$COMMAND" in
  45:     *"git commit"* | *"git -"*" commit"* ) ;;
  46:     *) echo '{}'; exit 0 ;;
  47: esac
  48: 
  49: cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }
  50: 
  51: # Files in the commit. No blanket --amend skip (amend is commonly "oops, add a
  52: # file" -- exactly the case to catch); include HEAD's files on amend.
  53: STAGED=$(git diff --cached --name-only 2>/dev/null)
  54: case "$COMMAND" in
  55:     *"--amend"*)
  56:         STAGED=$(printf '%s\n%s\n' "$STAGED" \
  57:             "$(git show HEAD --name-only --pretty=format: 2>/dev/null)" | sort -u)
  58:         ;;
  59: esac
  60: 
  61: # Only run when the commit touches ModuleData XML (the validator's scope).
  62: HAS_MD=0
  63: while IFS= read -r f; do
  64:     case "$f" in
  65:         Main/_Module/ModuleData/*.xml) HAS_MD=1; break ;;
  66:     esac
  67: done <<< "$STAGED"
  68: [[ $HAS_MD -eq 0 ]] && { echo '{}'; exit 0; }
  69: 
  70: # Locate python (fail open if absent).
  71: PY=$(command -v python3 || command -v python || true)
  72: [[ -z "$PY" ]] && { echo '{}'; exit 0; }
  73: 
  74: # Run only the ERROR-severity checks. Validator exits 1 on ERROR, 0 clean,
  75: # 2 bad-input. Only rc=1 blocks; everything else fails open.
  76: OUT=$("$PY" tools/validate_moduledata.py \
  77:         --code BROKEN_ITEM_REF --code BROKEN_TROOP_REF --code UNKNOWN_CULTURE \
  78:         --code DUPLICATE_NPC_ID --code DUPLICATE_CULTURE_ID --code DUPLICATE_ROSTER_ID \
  79:         2>/dev/null)
  80: RC=$?
  81: [[ $RC -ne 1 ]] && { echo '{}'; exit 0; }
  82: 
  83: # Build a JSON-escaped deny message with the validator's findings (bounded).
  84: MSG=$(printf '%s' "$OUT" | python3 -c '
  85: import sys, json
  86: lines = [l for l in sys.stdin.read().splitlines() if l.strip()][-30:]
  87: print(json.dumps(
  88:     "[check-moduledata-validation] git commit BLOCKED: tools/validate_moduledata.py "
  89:     "found ERROR-severity issues in staged ModuleData XML (broken Item/NPCCharacter "
  90:     "ref, unknown culture, or duplicate id). Fix them and re-stage. See details: "
  91:     "python tools/validate_moduledata.py\n\n" + "\n".join(lines)))
  92: ' 2>/dev/null)
  93: [[ -z "$MSG" ]] && { echo '{}'; exit 0; }
  94: 
  95: printf '{"permissionDecision":"deny","message":%s}\n' "$MSG"
  96: exit 0

 succeeded in 438ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:23: <UseDebugLibraries>true</UseDebugLibraries>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:29: <UseDebugLibraries>false</UseDebugLibraries>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:64: <AdditionalIncludeDirectories>$(ProjectDir)MinHook\include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:70: <RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:77: <AdditionalLibraryDirectories>$(ProjectDir)MinHook\lib</AdditionalLibraryDirectories>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:78: <AdditionalDependencies>MinHook.x64.lib;%(AdditionalDependencies)</AdditionalDependencies>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:92: <AdditionalIncludeDirectories>$(ProjectDir)MinHook\include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:93: <RuntimeLibrary>MultiThreaded</RuntimeLibrary>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:102: <AdditionalLibraryDirectories>$(ProjectDir)MinHook\lib</AdditionalLibraryDirectories>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:103: <AdditionalDependencies>MinHook.x64.lib;%(AdditionalDependencies)</AdditionalDependencies>
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:130: <!-- Post-build: copy MinHook.x64.dll alongside the output so loading the
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:133: <Target Name="CopyMinHookSidecar" AfterTargets="Build">
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\NativeSkinFixes.NativeHooks.vcxproj:134: <Copy SourceFiles="$(ProjectDir)MinHook\lib\MinHook.x64.dll" DestinationFolder="$(OutDir)" SkipUnchangedFiles="true" />

2026-06-18T19:57:01.028153Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path .gitignore,Dependencies/NativeSkinFixes.NativeHoo[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\.gitignore' because it does not exist.[0m

 exited 1 in 502ms:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path .gitignore,Dependencies/NativeSkinFixes.NativeHoo[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\.gitignore' because it does not exist.[0m

 succeeded in 423ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:2: using System.IO;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:9: /// Loads <c>TAOM.NativeSkinFixes.dll</c> + its MinHook dependency from the
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:20: /// <para>Idempotent: <see cref="EnsureLoaded"/> is safe to call multiple times.
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:38: private static string? _lastLoadError;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:44: public static string? LastLoadError => _lastLoadError;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:50: public static bool EnsureLoaded()
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:63: _lastLoadError = $"ModuleHelper.GetModuleFullPath threw: {ex.GetType().Name}: {ex.Message}";
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:69: // Setting the DLL directory so MinHook.x64.dll resolves alongside the
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:85: _lastLoadError = $"LoadLibrary failed (Win32 error {err}). Expected DLL at: {pluginPath}";
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:89: bool minHookPresent = File.Exists(Path.Combine(binDir, "MinHook.x64.dll"));
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:90: _lastLoadError += $" (error 126 = a module in the dependency chain is missing." +
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:91: $" plugin present: {pluginPresent}, MinHook.x64.dll present: {minHookPresent}." +

codex
The native project is scoped the way the prompt describes: Debug gets `MultiThreadedDebug`, Release already has `MultiThreaded`, and MinHook remains a sidecar import via `MinHook.x64.lib`. I’m checking for any gap where those facts are documented incorrectly or where CI/hook checks don’t exercise the committed artifact.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path .gitignore -Pattern 'TAOM.NativeSkinFixes|MinHook|Win64_Shipping_Client' -ErrorAction SilentlyContinue | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git ls-files Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll Dependencies/NativeSkinFixes.NativeHooks/MinHook/lib/MinHook.x64.dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'python tools/pe_inspect.py Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll Dependencies/NativeSkinFixes.NativeHooks/MinHook/lib/MinHook.x64.dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff --cached -- .claude/hooks/check-native-dll-crt.sh .claude/settings.json .github/workflows/build.yml Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs docs/features/native-skin-fixes.md CHANGELOG.md' in C:\Users\mikew\source\repos\TAOM
 succeeded in 348ms:
Dependencies/NativeSkinFixes.NativeHooks/MinHook/lib/MinHook.x64.dll
Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll
Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll

 succeeded in 342ms:
MinHook.x64.dll
  machine=x64  PE32+  sections=6
  internal_name=MinHook.x64.dll  exports: 12 funcs / 12 named
  sample exports: MH_ApplyQueued, MH_CreateHook, MH_CreateHookApi, MH_CreateHookApiEx, MH_DisableHook, MH_EnableHook, MH_Initialize, MH_QueueDisableHook, MH_QueueEnableHook, MH_RemoveHook, MH_StatusToString, MH_Uninitialize
  imports (1): KERNEL32.dll

MinHook.x64.dll
  machine=x64  PE32+  sections=6
  internal_name=MinHook.x64.dll  exports: 12 funcs / 12 named
  sample exports: MH_ApplyQueued, MH_CreateHook, MH_CreateHookApi, MH_CreateHookApiEx, MH_DisableHook, MH_EnableHook, MH_Initialize, MH_QueueDisableHook, MH_QueueEnableHook, MH_RemoveHook, MH_StatusToString, MH_Uninitialize
  imports (1): KERNEL32.dll


 succeeded in 329ms:
diff --git a/.claude/hooks/check-native-dll-crt.sh b/.claude/hooks/check-native-dll-crt.sh
new file mode 100755
index 00000000..d317238e
--- /dev/null
+++ b/.claude/hooks/check-native-dll-crt.sh
@@ -0,0 +1,96 @@
+#!/usr/bin/env bash
+# check-native-dll-crt.sh
+# PreToolUse(Bash) hook: when `git commit` stages the vendored native
+# TAOM.NativeSkinFixes.dll, run tools/pe_inspect.py and BLOCK the commit if the
+# DLL links a DYNAMIC C runtime (imports vcruntime*/msvcp140*/ucrtbase*/
+# api-ms-win-crt*). A dynamic/debug CRT is absent on players' machines without
+# Visual Studio, so LoadLibrary fails with Win32 error 126 and the feature goes
+# inert. The DLL MUST link a static CRT (Debug /MTd or Release /MT).
+#
+# Defense in depth: Build.ps1 already guards its own output, but this catches a
+# hand-copied or stale debug DLL that bypasses Build.ps1. See
+# docs/features/native-skin-fixes.md "Build & CRT requirement".
+#
+# Fail-open (per .claude/rules/harness-facts.md "TAOM hooks MUST fail open"):
+# no python, no pe_inspect.py, DLL not staged, DLL absent on disk, or any
+# internal error ALLOWS the commit. Only a confirmed dynamic-CRT import blocks.
+#
+# Returns: {} to allow, {"permissionDecision":"deny","message":"..."} to block.
+
+set -uo pipefail
+
+INPUT=$(cat)
+
+# Extract the bash command from tool_input (mirrors check-moduledata-validation.sh).
+COMMAND=$(printf '%s' "$INPUT" | python3 -c '
+import sys, json
+try:
+    d = json.loads(sys.stdin.read())
+    print(d.get("tool_input", {}).get("command", ""))
+except Exception:
+    pass
+' 2>/dev/null)
+
+# Two-stage git-commit matcher: handle `git -C/-c ... commit`; reject
+# `git commit-tree` / `commit-graph`. Per .claude/rules/harness-facts.md.
+case "$COMMAND" in
+    *"git commit-"*) echo '{}'; exit 0 ;;
+esac
+case "$COMMAND" in
+    *"git commit"* | *"git -"*" commit"* ) ;;
+    *) echo '{}'; exit 0 ;;
+esac
+
+cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }
+
+DLL="Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll"
+
+# Only run when this commit stages the vendored DLL. No blanket --amend skip
+# (amend is commonly "oops, add a file" -- include HEAD's files on amend).
+STAGED=$(git diff --cached --name-only 2>/dev/null)
+case "$COMMAND" in
+    *"--amend"*)
+        STAGED=$(printf '%s\n%s\n' "$STAGED" \
+            "$(git show HEAD --name-only --pretty=format: 2>/dev/null)" | sort -u)
+        ;;
+esac
+
+HAS_DLL=0
+while IFS= read -r f; do
+    [[ "$f" == "$DLL" ]] && { HAS_DLL=1; break; }
+done <<< "$STAGED"
+[[ $HAS_DLL -eq 0 ]] && { echo '{}'; exit 0; }
+
+# Fail open if we can't run the check.
+[[ -f "$DLL" ]] || { echo '{}'; exit 0; }
+PY=$(command -v python3 || command -v python || true)
+[[ -z "$PY" ]] && { echo '{}'; exit 0; }
+[[ -f tools/pe_inspect.py ]] || { echo '{}'; exit 0; }
+
+IMPORTS=$("$PY" tools/pe_inspect.py "$DLL" 2>/dev/null)
+[[ -z "$IMPORTS" ]] && { echo '{}'; exit 0; }
+
+# Dynamic CRT imports => the redistributable/debug runtime players lack. A
+# static-CRT build (Debug /MTd or Release /MT) imports only MinHook.x64.dll +
+# KERNEL32.dll and never matches this.
+if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP140|ucrtbase|api-ms-win-crt'; then
+    echo '{}'; exit 0
+fi
+
+# Build a JSON-escaped deny message with the import list (bounded).
+MSG=$(printf '%s' "$IMPORTS" | python3 -c '
+import sys, json
+lines = [l for l in sys.stdin.read().splitlines() if l.strip()][-12:]
+print(json.dumps(
+    "[check-native-dll-crt] git commit BLOCKED: the vendored "
+    "Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll links a "
+    "DYNAMIC C runtime (imports below). Players without Visual Studio lack those "
+    "DLLs, so LoadLibrary fails with Win32 error 126 and NativeSkinFixes goes "
+    "inert. Rebuild with a STATIC CRT (Debug /MTd or Release /MT) via "
+    "pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1, then re-stage.\n\n"
+    + "\n".join(lines)))
+' 2>/dev/null)
+[[ -z "$MSG" ]] && { echo '{}'; exit 0; }
+
+printf '{"permissionDecision":"deny","message":%s}\n' "$MSG"
+exit 0

 succeeded in 502ms:
C:\Users\mikew\source\repos\TAOM\.gitignore:26: !Dependencies/_Module/bin/Win64_Shipping_Client/
C:\Users\mikew\source\repos\TAOM\.gitignore:32: Dependencies/_Module/bin/Win64_Shipping_Client/*
C:\Users\mikew\source\repos\TAOM\.gitignore:33: !Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ButterLib*.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:34: !Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.MBOptionScreen*.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:35: !Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.ModuleLoader.*.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:36: !Dependencies/_Module/bin/Win64_Shipping_Client/BUTR.CrashReport*.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:37: !Dependencies/_Module/bin/Win64_Shipping_Client/MCM.UI.Adapter.*.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:38: !Dependencies/_Module/bin/Win64_Shipping_Client/Microsoft.Extensions.*.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:39: !Dependencies/_Module/bin/Win64_Shipping_Client/Microsoft.Bcl.HashCode.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:40: !Dependencies/_Module/bin/Win64_Shipping_Client/Serilog*.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:41: !Dependencies/_Module/bin/Win64_Shipping_Client/System.Buffers.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:42: !Dependencies/_Module/bin/Win64_Shipping_Client/System.Collections.Immutable.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:43: !Dependencies/_Module/bin/Win64_Shipping_Client/System.Memory.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:44: !Dependencies/_Module/bin/Win64_Shipping_Client/System.Numerics.Vectors.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:45: !Dependencies/_Module/bin/Win64_Shipping_Client/System.Reflection.Metadata.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:51: # Bannerlord.MCM NuGet — NOT vendored here. TAOM.NativeSkinFixes.dll is
C:\Users\mikew\source\repos\TAOM\.gitignore:55: !Main/_Module/bin/Win64_Shipping_Client/
C:\Users\mikew\source\repos\TAOM\.gitignore:56: Main/_Module/bin/Win64_Shipping_Client/*
C:\Users\mikew\source\repos\TAOM\.gitignore:60: !Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll
C:\Users\mikew\source\repos\TAOM\.gitignore:61: !Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll

codex
One real residual dependency is that the guards are token scanners over `pe_inspect.py` text, not structured consumers. The current inspector prints every imported DLL on line 77, so there is no current truncation gap, but I’m checking how the guards behave if that output becomes malformed or absent.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'bash --version' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$tokens = @('"'VCRUNTIME140.dll','VCRUNTIME140D.dll','VCRUNTIME140_1.dll','VCRUNTIME140_1D.dll','MSVCP140.dll','MSVCP140D.dll','MSVCP150.dll','MSVCR120.dll','ucrtbase.dll','ucrtbased.dll','api-ms-win-crt-runtime-l1-1-0.dll','MinHook.x64.dll','KERNEL32.dll','SHELL32.dll','ole32.dll'); foreach ("'$t in $tokens) { '"'{0}: {1}' -f "'$t, ([bool]($t -match '"'(?i)VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt')) }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "bash -lc \"for t in VCRUNTIME140.dll VCRUNTIME140D.dll VCRUNTIME140_1.dll VCRUNTIME140_1D.dll MSVCP140.dll MSVCP140D.dll MSVCP150.dll MSVCR120.dll ucrtbase.dll ucrtbased.dll api-ms-win-crt-runtime-l1-1-0.dll MinHook.x64.dll KERNEL32.dll SHELL32.dll ole32.dll; do if printf '%s' \\\""'$t'"\\\" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then echo \\\""'$t: true'"\\\"; else echo \\\""'$t: false'"\\\"; fi; done\"" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path CHANGELOG.md -Pattern 'fix\\(native-skin-fixes\\)|MSVCP140D|MultiThreadedDebug|pe_inspect|MinHook.x64.dll|SHELL32|ole32|check-native-dll-crt|<PATTERN_TBD>' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 353ms:
VCRUNTIME140.dll: True
VCRUNTIME140D.dll: True
VCRUNTIME140_1.dll: True
VCRUNTIME140_1D.dll: True
MSVCP140.dll: True
MSVCP140D.dll: True
MSVCP150.dll: True
MSVCR120.dll: True
ucrtbase.dll: True
ucrtbased.dll: True
api-ms-win-crt-runtime-l1-1-0.dll: True
MinHook.x64.dll: False
KERNEL32.dll: False
SHELL32.dll: False
ole32.dll: False

2026-06-18T19:57:59.176020Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
      0 [main] bash (51708) C:\Program Files\Git\usr\bin\bash.exe: *** fatal error - couldn't create signal pipe, Win32 error 5

 exited 1 in 387ms:
      0 [main] bash (51708) C:\Program Files\Git\usr\bin\bash.exe: *** fatal error - couldn't create signal pipe, Win32 error 5

2026-06-18T19:57:59.234852Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
[31;1mParserError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m . P[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then echo \"[36;1m$t:[0m true\"; .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                                             ~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mVariable reference is not valid. ':' was not followed by a valid variable name character. Consider using ${} to[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1m[36;1m     | [31;1mdelimit the name.[0m

 exited 1 in 446ms:
[31;1mParserError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m . P[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then echo \"[36;1m$t:[0m true\"; .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                                             ~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mVariable reference is not valid. ':' was not followed by a valid variable name character. Consider using ${} to[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1m[36;1m     | [31;1mdelimit the name.[0m

 succeeded in 469ms:
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:31: ### fix(native-skin-fixes): ship a static-CRT DLL so it loads for players (Win32 error 126)
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:35: missing. `python tools/pe_inspect.py` on the vendored DLL proved the cause: it imported `MSVCP140D.dll`,
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:40: not the debug CRT). `MinHook.x64.dll` was never the culprit — it imports only `KERNEL32.dll`.
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:43: `<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>` (`/MTd`, static debug CRT) to match Release's `/MT`.
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:44: The DLL was rebuilt and re-vendored; `pe_inspect` confirms it now imports only `MinHook.x64.dll`,
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:45: `KERNEL32.dll`, and the OS-guaranteed `SHELL32.dll`/`ole32.dll` — no redistributable dependency. Three
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:46: guards prevent the regression from recurring: `Build.ps1` runs `pe_inspect` on its own output and throws
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:47: on any dynamic-CRT import; the new `check-native-dll-crt.sh` PreToolUse hook blocks a commit that stages a
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:49: message now reports whether the plugin + `MinHook.x64.dll` are present and points at the static-CRT
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:51: signatures remain `<PATTERN_TBD>` placeholders (separate open follow-up) — this fix makes the DLL loadable,
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:1955: - **`tools/pe_inspect.py`** + the toolchain doc's "Verified facts" section — a stdlib PE export/import parser to "see into" NATIVE DLLs when ilspycmd can't (facts, not guesses). Confirmed the engine's real tech stack from `TaleWorlds.Native.dll`'s import table: **Mono runtime, NVIDIA PhysX physics, Granite virtual texturing, DirectX 11, NVIDIA NGX/DLSS**; the managed↔native bootstrap exports (`WotsMain*`/`pass_managed_*`); and exact toolchain versions (Qt 5.11.2, FBX SDK 2016, Embree 3.6.1, TBB 2018, FreeImage 3.17).
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:3791: - **Open follow-up:** the 7 byte patterns ship as `<PATTERN_TBD>` placeholders. The scanner architecture is verified end-to-end (compiles, tests green, scanner returns 0 + logs cleanly on stub patterns). Authoring the v1.4.5 patterns is a one-time ~30 min IDA session — see the feature doc's "Pattern authoring" section. Until authored, hooks log "pattern not authored for this build (stub)" and stay inert.
C:\Users\mikew\source\repos\TAOM\CHANGELOG.md:4836: Adds `BehaviorTrees.dll`, `BehaviorTreeWrapper.dll`, `MinHook.x64.dll`, and `TAOM.NativeSkinFixes.dll` to the repo via `.gitignore` allowlist (same pattern as `Dependencies/_Module/bin/`). Fresh clones and CI can now build — previously these vendored DLLs were caught by the top-level `bin/` ignore and had to be sideloaded by hand on every machine. The `Bannerlord.BuildResources` `PostBuildCopyToModules` target already mirrors the folder into the Steam install on every build, so commits to these DLLs (e.g., when `TAOM.NativeSkinFixes.dll` is recompiled externally) now propagate to teammates automatically.

codex
PowerShell regex matched all expected CRT tokens, including future-numbered `MSVCP150.dll`, and did not match the current static imports. Git Bash is failing to start in this sandbox, so I’m treating the Bash side by POSIX ERE semantics plus the script text rather than by local execution.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Command grep -ErrorAction SilentlyContinue | Format-List Source,Version' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content .gitignore | ForEach-Object { $i++; if ($i -ge 50 -and $i -le 62) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff --cached --name-only' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff --cached --stat -- Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll' in C:\Users\mikew\source\repos\TAOM
 succeeded in 323ms:
.claude/hooks/check-native-dll-crt.sh

 succeeded in 443ms:

[32;1mSource  : [0mC:\Program Files\Git\usr\bin\grep.exe
[32;1mVersion : [0m0.0.0.0


 succeeded in 313ms:
 succeeded in 485ms:
  50: # by `dotnet build`). MCMv5.dll is provided by TAOM.Dependencies + the
  51: # Bannerlord.MCM NuGet — NOT vendored here. TAOM.NativeSkinFixes.dll is
  52: # TAOM-owned C++ rebuilt outside this repo; drop the new .dll here and
  53: # commit when it changes.
  54: !Main/_Module/bin/
  55: !Main/_Module/bin/Win64_Shipping_Client/
  56: Main/_Module/bin/Win64_Shipping_Client/*
  57: # BehaviorTrees.dll + BehaviorTreeWrapper.dll allowlists removed 2026-05-24 —
  58: # both libraries are now inlined in TAOM source (Main/BehaviorTrees/ +
  59: # Main/BehaviorTreeWrapper/) and compile into TAOM.dll. No standalone DLLs.
  60: !Main/_Module/bin/Win64_Shipping_Client/MinHook.x64.dll
  61: !Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll
  62: # Gaming.Desktop sibling: build outputs only — stays ignored.

codex
One process detail surfaced: only the new hook file is staged right now; the rest of the reviewed changes are unstaged. I’m still reviewing the working-tree contents because that is what the prompt describes, but this matters before any actual commit.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path .claude/hooks/check-native-dll-crt.sh,Dependencies/NativeSkinFixes.NativeHooks/Build.ps1,.github/workflows/build.yml -Pattern 'VCRUNTIME|MSVCP|MSVCR|ucrtbase|api-ms-win-crt|pe_inspect|IMPORTS|imports' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path tools/pe_inspect.py -Pattern 'max_names|imports|sample exports|imps|join' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$grep = (Get-Command grep).Source; $tokens = @('"'VCRUNTIME140.dll','VCRUNTIME140D.dll','VCRUNTIME140_1.dll','VCRUNTIME140_1D.dll','MSVCP140.dll','MSVCP140D.dll','MSVCP150.dll','MSVCR120.dll','ucrtbase.dll','ucrtbased.dll','api-ms-win-crt-runtime-l1-1-0.dll','MinHook.x64.dll','KERNEL32.dll','SHELL32.dll','ole32.dll'); foreach ("'$t in $tokens) { $t | & $grep -iqE '"'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; "'$m = if ($LASTEXITCODE -eq 0) { '"'true' } else { 'false' }; '{0}: {1}' -f "'$t, $m }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Dependencies/NativeSkinFixes.NativeHooks/**/*.cpp,Dependencies/NativeSkinFixes.NativeHooks/**/*.h,Dependencies/NativeSkinFixes.NativeHooks/*.cpp,Dependencies/NativeSkinFixes.NativeHooks/*.h -Pattern 'malloc|free|new |delete|FILE\\*|fopen|std::string|std::vector|extern \"C\"|__declspec\\(dllexport\\)' -ErrorAction SilentlyContinue | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 490ms:
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:27: def inspect(path, max_names=60):
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:59: for i in range(min(nnames, max_names)):
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:64: out.append("  sample exports: " + ", ".join(names[:max_names]))
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:67: # imports (dependencies)
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:70: imps = []
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:75: imps.append(cstr(d, rva_to_off(nm_rva, sections)))
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:77: out.append(f"  imports ({len(imps)}): " + ", ".join(imps))
C:\Users\mikew\source\repos\TAOM\tools\pe_inspect.py:78: return "\n".join(out)

 succeeded in 508ms:
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:4: # TAOM.NativeSkinFixes.dll, run tools/pe_inspect.py and BLOCK the commit if the
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:5: # DLL links a DYNAMIC C runtime (imports vcruntime*/msvcp140*/ucrtbase*/
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:6: # api-ms-win-crt*). A dynamic/debug CRT is absent on players' machines without
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:15: # no python, no pe_inspect.py, DLL not staged, DLL absent on disk, or any
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:68: [[ -f tools/pe_inspect.py ]] || { echo '{}'; exit 0; }
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:70: IMPORTS=$("$PY" tools/pe_inspect.py "$DLL" 2>/dev/null)
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:71: [[ -z "$IMPORTS" ]] && { echo '{}'; exit 0; }
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:73: # Dynamic CRT imports => the redistributable/debug runtime players lack. A
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:74: # static-CRT build (Debug /MTd or Release /MT) imports only MinHook.x64.dll +
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:76: if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:81: MSG=$(printf '%s' "$IMPORTS" | python3 -c '
C:\Users\mikew\source\repos\TAOM\.claude\hooks\check-native-dll-crt.sh:87: "DYNAMIC C runtime (imports below). Players without Visual Studio lack those "
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:60: # Release=/MT). A dynamic CRT (/MDd or /MD) imports vcruntime*/msvcp140*/
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:61: # ucrtbase*/api-ms-win-crt* — DLLs players without Visual Studio lack — and
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:64: $peInspect = Join-Path $scriptDir '..\..\tools\pe_inspect.py'
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:68: $imports = & $py.Source $peInspect $dllPath 2>&1 | Out-String
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:69: if ($imports -match '(?i)VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt') {
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:70: Write-Host $imports
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:71: throw "[NativeSkinFixes] BUILD REJECTED: $dllPath links a DYNAMIC CRT (see imports above). Rebuild with a static CRT (Debug=/MTd, Release=/MT). The dynamic/debug CRT is absent on players' machines -> LoadLibrary error 126."
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Build.ps1:75: Write-Host "[NativeSkinFixes] WARNING: python or tools/pe_inspect.py not found; skipped static-CRT import check (the commit hook + CI still gate it)." -ForegroundColor Yellow
C:\Users\mikew\source\repos\TAOM\.github\workflows\build.yml:81: # (Debug /MTd or Release /MT). A dynamic CRT (/MDd or /MD) imports
C:\Users\mikew\source\repos\TAOM\.github\workflows\build.yml:82: # vcruntime*/msvcp140*/ucrtbase*/api-ms-win-crt* — DLLs players
C:\Users\mikew\source\repos\TAOM\.github\workflows\build.yml:84: # error 126 and NativeSkinFixes goes inert. pe_inspect.py is pure
C:\Users\mikew\source\repos\TAOM\.github\workflows\build.yml:91: IMPORTS=$(python3 tools/pe_inspect.py "$DLL")
C:\Users\mikew\source\repos\TAOM\.github\workflows\build.yml:92: echo "$IMPORTS"
C:\Users\mikew\source\repos\TAOM\.github\workflows\build.yml:93: if echo "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
C:\Users\mikew\source\repos\TAOM\.github\workflows\build.yml:94: echo "::error::$DLL links a DYNAMIC CRT (imports above). Players without Visual Studio get LoadLibrary error 126. Rebuild with a static CRT — Debug /MTd or Release /MT — via Dependencies/NativeSkinFixes.NativeHooks/Build.ps1."

 succeeded in 597ms:
VCRUNTIME140.dll: false
VCRUNTIME140D.dll: false
VCRUNTIME140_1.dll: false
VCRUNTIME140_1D.dll: false
MSVCP140.dll: false
MSVCP140D.dll: false
MSVCP150.dll: false
MSVCR120.dll: false
ucrtbase.dll: false
ucrtbased.dll: false
api-ms-win-crt-runtime-l1-1-0.dll: false
MinHook.x64.dll: false
KERNEL32.dll: false
SHELL32.dll: false
ole32.dll: false
      0 [main] grep (71364) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (54396) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      1 [main] grep (31080) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (28652) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (32120) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (12936) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (68140) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (28920) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (4276) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (32320) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (68888) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (46384) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (52900) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (52808) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5
      0 [main] grep (62840) C:\Program Files\Git\usr\bin\grep.exe: *** fatal error - couldn't create signal pipe, Win32 error 5

 succeeded in 446ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.cpp:17: //   functions never run, which freezes hand grip morphs.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.cpp:94: // Update the hidden set with the new Face_mesh
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:13: FILE*    g_file = nullptr;
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:30: CoTaskMemFree(docs);
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:33: CoTaskMemFree(docs);
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:57: _wfopen_s(&g_file, path, L"w");
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:3: extern "C"
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:10: // initialized, which freezes hand morphs.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:19: __declspec(dllexport) bool __cdecl CoversHeadHook_Install();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:22: __declspec(dllexport) void __cdecl CoversHeadHook_Uninstall();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.h:3: extern "C"
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.h:14: __declspec(dllexport) bool __cdecl FaceMeshObserveHook_Install();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.h:17: __declspec(dllexport) void __cdecl FaceMeshObserveHook_Uninstall();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:3: extern "C"
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:14: __declspec(dllexport) bool __cdecl HairClothHook_Install();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:17: __declspec(dllexport) void __cdecl HairClothHook_Uninstall();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:21: __declspec(dllexport) bool __cdecl HairClothHook_HasBeardCloth(uintptr_t faceMeshPtr);
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Signatures.h:35: // IDA-style byte pattern when porting to a new engine version.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Signatures.h:58: //   never initializes and hand-grip morphs freeze.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\SignatureScanner.h:17: // should not free the returned base — modules stay loaded for the process

codex
The exported C++ surface is simple C ABI (`bool`/`void`/`uintptr_t`) and does not pass CRT-owned allocations across the managed/native boundary. That makes the “two CRTs in one process” concern acceptable here; the usual hazard is sharing `FILE*`, heap blocks, STL objects, or CRT state across module boundaries.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Dependencies/NativeSkinFixes.NativeHooks/HairClothHook.h | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Dependencies/NativeSkinFixes.NativeHooks/FaceMeshObserveHook.h | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Dependencies/NativeSkinFixes.NativeHooks/Logging.cpp | ForEach-Object { $i++; if ($i -le 90) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Dependencies/NativeSkinFixes.NativeHooks/CoversHeadHook.h | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 447ms:
   1: #pragma once
   2: 
   3: extern "C"
   4: {
   5:     // Hooks the Face_mesh render list builder. Two suppression modes:
   6:     //   1. covers_head: suppresses ALL face components (+0x100..+0x118) when
   7:     //      CoversHeadHook marks the Face_mesh as hidden.
   8:     //   2. cloth hair: suppresses only hair (+0x110) when cloth exists at +0x1A0,
   9:     //      so animated hair from HairClothHook renders instead of static hair.
  10:     //
  11:     // The target function is found by signature scan (Signatures::kRenderListBuild).
  12:     // Returns true on success, false on signature-not-found / hook-create-failed
  13:     // / install error. The game keeps running on false — the hook is inert.
  14:     __declspec(dllexport) bool __cdecl FaceMeshObserveHook_Install();
  15: 
  16:     // Removes the hook. Safe to call even if Install failed.
  17:     __declspec(dllexport) void __cdecl FaceMeshObserveHook_Uninstall();
  18: }

 succeeded in 467ms:
   1: #pragma once
   2: 
   3: extern "C"
   4: {
   5:     // Hooks the cloth factory inside TaleWorlds.Native.dll. Two cases:
   6:     //   1. Hair cloth: rescued from Face_mesh+0x1A0 (created by ctor, never registered)
   7:     //   2. Beard cloth: created by calling the cloth factory directly with the beard
   8:     //      mesh at +0x108 (the factory normally skips Face_mesh internals)
   9:     //
  10:     // The target function and three helper functions (AddToList, GpuInit,
  11:     // HasClothData) are all found by signature scan; see Signatures.h.
  12:     // Returns true on success, false on signature-not-found / hook-create-failed
  13:     // / install error. The game keeps running on false — the hook is inert.
  14:     __declspec(dllexport) bool __cdecl HairClothHook_Install();
  15: 
  16:     // Removes the detour. Safe to call even if Install failed or was never called.
  17:     __declspec(dllexport) void __cdecl HairClothHook_Uninstall();
  18: 
  19:     // Returns true if the given Face_mesh has beard cloth registered via the factory.
  20:     // Called by FaceMeshObserveHook to suppress static beard from the render list.
  21:     __declspec(dllexport) bool __cdecl HairClothHook_HasBeardCloth(uintptr_t faceMeshPtr);
  22: }

 succeeded in 437ms:
   1: #include "pch.h"
   2: #include "Logging.h"
   3: #include <shlobj.h>
   4: #include <cstdio>
   5: #include <cstdarg>
   6: 
   7: #pragma comment(lib, "Shell32.lib")
   8: 
   9: namespace TAOM { namespace NativeHooks { namespace Logging {
  10: 
  11: namespace {
  12: 
  13: FILE*    g_file = nullptr;
  14: SRWLOCK  g_lock = SRWLOCK_INIT;
  15: bool     g_initialized = false;
  16: 
  17: bool BuildLogPath(wchar_t* outPath, size_t maxChars)
  18: {
  19:     // %USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\TAOM\NativeSkinFixes.log
  20:     PWSTR docs = nullptr;
  21:     HRESULT hr = SHGetKnownFolderPath(FOLDERID_Documents, 0, nullptr, &docs);
  22:     if (FAILED(hr) || docs == nullptr) return false;
  23: 
  24:     // Compose the directory; create each level if missing.
  25:     wchar_t dir[MAX_PATH];
  26:     if (swprintf_s(dir, MAX_PATH,
  27:             L"%s\\Mount and Blade II Bannerlord\\Logs\\TAOM",
  28:             docs) < 0)
  29:     {
  30:         CoTaskMemFree(docs);
  31:         return false;
  32:     }
  33:     CoTaskMemFree(docs);
  34: 
  35:     // CreateDirectoryW returns success-or-already-exists; we walk only the
  36:     // TAOM-specific leg since "Documents\Mount and Blade II Bannerlord\Logs"
  37:     // is created by the game itself.
  38:     wchar_t parent[MAX_PATH];
  39:     swprintf_s(parent, MAX_PATH, L"%s\\..", dir);
  40:     CreateDirectoryW(parent, nullptr);
  41:     CreateDirectoryW(dir, nullptr);
  42: 
  43:     return swprintf_s(outPath, maxChars, L"%s\\NativeSkinFixes.log", dir) > 0;
  44: }
  45: 
  46: }  // namespace
  47: 
  48: void Init()
  49: {
  50:     AcquireSRWLockExclusive(&g_lock);
  51:     if (g_initialized) { ReleaseSRWLockExclusive(&g_lock); return; }
  52:     g_initialized = true;
  53: 
  54:     wchar_t path[MAX_PATH];
  55:     if (BuildLogPath(path, MAX_PATH))
  56:     {
  57:         _wfopen_s(&g_file, path, L"w");
  58:     }
  59:     ReleaseSRWLockExclusive(&g_lock);
  60: 
  61:     if (g_file != nullptr)
  62:     {
  63:         LogLine("[NativeSkinFixes] log open");
  64:     }
  65:     else
  66:     {
  67:         OutputDebugStringA("[NativeSkinFixes] log file unavailable; debug-string only\n");
  68:     }
  69: }
  70: 
  71: void Close()
  72: {
  73:     AcquireSRWLockExclusive(&g_lock);
  74:     if (g_file != nullptr)
  75:     {
  76:         fputs("[NativeSkinFixes] log close\n", g_file);
  77:         fclose(g_file);
  78:         g_file = nullptr;
  79:     }
  80:     g_initialized = false;
  81:     ReleaseSRWLockExclusive(&g_lock);
  82: }
  83: 
  84: void LogLine(const char* fmt, ...)
  85: {
  86:     char buf[1024];
  87:     va_list args;
  88:     va_start(args, fmt);
  89:     int n = vsnprintf(buf, sizeof(buf), fmt, args);
  90:     va_end(args);

 succeeded in 441ms:
   1: #pragma once
   2: 
   3: extern "C"
   4: {
   5:     // Hooks add_skin_meshes_to_agent_entity inside TaleWorlds.Native.dll.
   6:     //
   7:     // When the SkinGenerationParams visibility mask has HeadVisible (bit 0x01)
   8:     // cleared (covers_head="true"), the engine skips creating the Face_mesh
   9:     // entirely. Without the Face_mesh, the GPU morph pipeline is never
  10:     // initialized, which freezes hand morphs.
  11:     //
  12:     // This hook forces bit 0x01 ON so the Face_mesh (and morph pipeline) is
  13:     // always created. Hidden faces are tracked in a set so the render list
  14:     // hook can suppress all face components from rendering.
  15:     //
  16:     // The target function is found by signature scan (Signatures::kAddSkinMeshes).
  17:     // Returns true on success, false on signature-not-found / hook-create-failed
  18:     // / install error. The game keeps running on false — the hook is just inert.
  19:     __declspec(dllexport) bool __cdecl CoversHeadHook_Install();
  20: 
  21:     // Removes the hook. Safe to call even if Install failed or was never called.
  22:     __declspec(dllexport) void __cdecl CoversHeadHook_Uninstall();
  23: }
  24: 
  25: // Internal API — called from FaceMeshObserveHook and HairClothHook
  26: // Returns true if the given Face_mesh should have all face rendering suppressed.
  27: bool CoversHeadHook_ShouldHideFace(uintptr_t faceMeshPtr);
  28: 
  29: // Returns true if the current thread is inside a covers_head skin mesh call.
  30: // Used during initial creation when the Face_mesh isn't yet in the set.
  31: bool CoversHeadHook_IsCreatingHidden();

codex
The two process guards are intentionally fail-open locally, so the hard stop is CI unless the maintainer bypasses it. That is acceptable defense-in-depth for local hooks, but it means any path that skips CI or ships from an unreviewed local artifact is still a residual release-process risk rather than something the code can fully prevent.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content .github/workflows/build.yml | ForEach-Object { $i++; if ($i -le 35) { '"'{0,4}: {1}' -f "'$i, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content Main/Features/NativeSkinFixes/Interop/NativeHookDelegates.cs -ErrorAction SilentlyContinue | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Dependencies/NativeSkinFixes.NativeHooks/*.cpp,Dependencies/NativeSkinFixes.NativeHooks/*.h -Pattern '__declspec\\(dllexport\\)|extern \"C\"|__cdecl|malloc|free|new |delete|std::|FILE\\*|fopen|fclose' -ErrorAction SilentlyContinue | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main/Features/NativeSkinFixes/**/*.cs -Pattern 'GetExport|delegate|NativeHookLoader|DllImport|Marshal' -ErrorAction SilentlyContinue | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
2026-06-18T19:59:04.269813Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:

 exited 1 in 445ms:
 succeeded in 477ms:
   1: name: Build & Test
   2: 
   3: on:
   4:   push:
   5:     branches: [master]
   6:   pull_request:
   7:     branches: [master]
   8: 
   9: env:
  10:   DOTNET_NOLOGO: true
  11:   DOTNET_CLI_TELEMETRY_OPTOUT: true
  12: 
  13: jobs:
  14:   check-build-config:
  15:     name: Check Build Configuration
  16:     runs-on: ubuntu-latest
  17:     steps:
  18:       - name: Warn if BANNERLORD_GAME_DIR unset
  19:         env:
  20:           BANNERLORD_GAME_DIR: ${{ vars.BANNERLORD_GAME_DIR }}
  21:         run: |
  22:           if [ -z "$BANNERLORD_GAME_DIR" ]; then
  23:             echo "::warning::BANNERLORD_GAME_DIR is not set — the Build & Test job will be skipped. Set this variable in repository settings to enable full CI."
  24:           fi
  25: 
  26:   validate-xml:
  27:     name: Validate XML & XSLT
  28:     runs-on: ubuntu-latest
  29:     steps:
  30:       - uses: actions/checkout@v4
  31: 
  32:       - name: Validate XML well-formedness
  33:         shell: pwsh
  34:         run: |
  35:           $errors = 0

 succeeded in 448ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.cpp:17: //   functions never run, which freezes hand grip morphs.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.cpp:42: static std::unordered_set<uintptr_t> g_hiddenFaces;
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.cpp:94: // Update the hidden set with the new Face_mesh
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.cpp:147: bool __cdecl CoversHeadHook_Install()
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.cpp:186: void __cdecl CoversHeadHook_Uninstall()
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.cpp:166: bool __cdecl FaceMeshObserveHook_Install()
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.cpp:205: void __cdecl FaceMeshObserveHook_Uninstall()
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.cpp:73: static std::unordered_set<uintptr_t> g_beardClothFaces;
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.cpp:281: bool __cdecl HairClothHook_HasBeardCloth(uintptr_t faceMeshPtr)
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.cpp:305: bool __cdecl HairClothHook_Install()
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.cpp:390: void __cdecl HairClothHook_Uninstall()
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:13: FILE*    g_file = nullptr;
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:30: CoTaskMemFree(docs);
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:33: CoTaskMemFree(docs);
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:57: _wfopen_s(&g_file, path, L"w");
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Logging.cpp:77: fclose(g_file);
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:3: extern "C"
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:10: // initialized, which freezes hand morphs.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:19: __declspec(dllexport) bool __cdecl CoversHeadHook_Install();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\CoversHeadHook.h:22: __declspec(dllexport) void __cdecl CoversHeadHook_Uninstall();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.h:3: extern "C"
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.h:14: __declspec(dllexport) bool __cdecl FaceMeshObserveHook_Install();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\FaceMeshObserveHook.h:17: __declspec(dllexport) void __cdecl FaceMeshObserveHook_Uninstall();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:3: extern "C"
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:14: __declspec(dllexport) bool __cdecl HairClothHook_Install();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:17: __declspec(dllexport) void __cdecl HairClothHook_Uninstall();
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\HairClothHook.h:21: __declspec(dllexport) bool __cdecl HairClothHook_HasBeardCloth(uintptr_t faceMeshPtr);
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Signatures.h:35: // IDA-style byte pattern when porting to a new engine version.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\Signatures.h:58: //   never initializes and hand-grip morphs freeze.
C:\Users\mikew\source\repos\TAOM\Dependencies\NativeSkinFixes.NativeHooks\SignatureScanner.h:17: // should not free the returned base — modules stay loaded for the process

 succeeded in 462ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\CoversHeadHookInterop.cs:16: private delegate bool InstallDelegate();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\CoversHeadHookInterop.cs:19: private delegate void UninstallDelegate();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\CoversHeadHookInterop.cs:21: private static InstallDelegate? _install;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\CoversHeadHookInterop.cs:22: private static UninstallDelegate? _uninstall;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\CoversHeadHookInterop.cs:30: _install   = NativeHookLoader.GetExport<InstallDelegate>("CoversHeadHook_Install");
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\CoversHeadHookInterop.cs:31: _uninstall = NativeHookLoader.GetExport<UninstallDelegate>("CoversHeadHook_Uninstall");
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\FaceMeshObserveHookInterop.cs:16: private delegate bool InstallDelegate();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\FaceMeshObserveHookInterop.cs:19: private delegate void UninstallDelegate();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\FaceMeshObserveHookInterop.cs:21: private static InstallDelegate? _install;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\FaceMeshObserveHookInterop.cs:22: private static UninstallDelegate? _uninstall;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\FaceMeshObserveHookInterop.cs:30: _install   = NativeHookLoader.GetExport<InstallDelegate>("FaceMeshObserveHook_Install");
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\FaceMeshObserveHookInterop.cs:31: _uninstall = NativeHookLoader.GetExport<UninstallDelegate>("FaceMeshObserveHook_Uninstall");
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\HairClothHookInterop.cs:17: private delegate bool InstallDelegate();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\HairClothHookInterop.cs:20: private delegate void UninstallDelegate();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\HairClothHookInterop.cs:22: private static InstallDelegate? _install;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\HairClothHookInterop.cs:23: private static UninstallDelegate? _uninstall;
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\HairClothHookInterop.cs:31: _install   = NativeHookLoader.GetExport<InstallDelegate>("HairClothHook_Install");
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\HairClothHookInterop.cs:32: _uninstall = NativeHookLoader.GetExport<UninstallDelegate>("HairClothHook_Uninstall");
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:11: /// <c>GetExport&lt;T&gt;</c> for the three hook installers to resolve their
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:23: internal static class NativeHookLoader
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:27: [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:30: [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:33: [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:83: int err = Marshal.GetLastWin32Error();
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:102: /// Resolves a DLL export to a typed delegate. Returns null if the export
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:105: public static T? GetExport<T>(string name)
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:106: where T : Delegate
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\Interop\NativeHookLoader.cs:111: return Marshal.GetDelegateForFunctionPointer<T>(proc);

codex
One important distinction for release safety is working tree versus committed artifact. The working-tree DLL is static, but the binary is currently unstaged, so I’m checking the HEAD/index artifact separately to avoid accidentally reviewing a file that would not be in the next commit.
2026-06-18T20:00:34.189742Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$tmp='"'C:\\tmp\\TAOM.NativeSkinFixes.HEAD.dll'; git show HEAD:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > "'$tmp; python tools/pe_inspect.py $tmp; Remove-Item -LiteralPath $tmp -Force'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$tmp='"'C:\\tmp\\TAOM.NativeSkinFixes.HEAD.dll'; git show HEAD:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > "'$tmp; python tools/pe_inspect.py $tmp; Remove-Item -LiteralPath $tmp -Force' in C:\Users\mikew\source\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$tmp='"'C:\\tmp\\TAOM.NativeSkinFixes.HEAD.dll'; git show HEAD:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > "'$tmp; python tools/pe_inspect.py $tmp; Remove-Item -LiteralPath $tmp -Force'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff --name-only -- Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll; git diff --cached --name-only -- Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff --numstat -- Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll; git diff --cached --numstat -- Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll' in C:\Users\mikew\source\repos\TAOM
2026-06-18T20:00:34.295631Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git show :Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll; python tools/pe_inspect.py C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll; Remove-Item -LiteralPath C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll -Force"` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git show :Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll; python tools/pe_inspect.py C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll; Remove-Item -LiteralPath C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll -Force" in C:\Users\mikew\source\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git show :Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll; python tools/pe_inspect.py C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll; Remove-Item -LiteralPath C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll -Force"` rejected: blocked by policy
 succeeded in 387ms:
Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll

 succeeded in 369ms:
-	-	Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll

codex
The sandbox blocked my cleanup command for a temporary binary snapshot, so I’m avoiding any destructive cleanup and only checking whether the DLL change is unstaged. The status already shows it is: the next commit would not include the rebuilt binary unless it is staged.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git diff --cached -- .claude/hooks/check-native-dll-crt.sh | Select-String -Pattern 'grep -iqE|MSVCP' -Context 1,1 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git show HEAD:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > C:\\tmp\\TAOM.NativeSkinFixes.HEAD.dll; python tools/pe_inspect.py C:\\tmp\\TAOM.NativeSkinFixes.HEAD.dll" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git show :Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll > C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll; python tools/pe_inspect.py C:\\tmp\\TAOM.NativeSkinFixes.INDEX.dll" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git diff -- .claude/hooks/check-native-dll-crt.sh | Select-String -Pattern 'grep -iqE|MSVCP' -Context 1,1 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 444ms:
[31;1mOpenError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mgit show :Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\TAOM.NativeSkinFixes.INDEX.dll' is denied.[0m

 succeeded in 469ms:
[31;1mOpenError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mgit show HEAD:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinF[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\TAOM.NativeSkinFixes.HEAD.dll' is denied.[0m

 succeeded in 505ms:
  +# TAOM.NativeSkinFixes.dll, run tools/pe_inspect.py and BLOCK the commit if the
> +# DLL links a DYNAMIC C runtime (imports vcruntime*/msvcp140*/ucrtbase*/
  +# api-ms-win-crt*). A dynamic/debug CRT is absent on players' machines without
  +# KERNEL32.dll and never matches this.
> +if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP140|ucrtbase|api-ms-win-crt'; then
  +    echo '{}'; exit 0

 succeeded in 482ms:
   # KERNEL32.dll and never matches this.
> -if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP140|ucrtbase|api-ms-win-crt'; then
> +if ! printf '%s' "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
       echo '{}'; exit 0
warning: in the working copy of '.claude/hooks/check-native-dll-crt.sh', LF will be replaced by CRLF the next time Git touches it

codex
The index check exposed a concrete staging mismatch: the staged hook still contains the older narrower `MSVCP140` regex, while the working tree has the intended `MSVCP[0-9]+|MSVCR[0-9]+` update. That is not a source-code logic defect in the working tree, but it is a real “do not commit yet” condition.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem C:\\tmp -Force | Select-Object -First 20 | Format-Table Mode,Length,Name" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Test-Path C:\\tmp\\TAOM.NativeSkinFixes.HEAD.dll; Get-Item C:\\tmp\\TAOM.NativeSkinFixes.HEAD.dll -Force -ErrorAction SilentlyContinue | Format-List FullName,Attributes,Length,Mode" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git hash-object Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll; git rev-parse :Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll; git rev-parse HEAD:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- .claude/settings.json .github/workflows/build.yml Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs docs/features/native-skin-fixes.md CHANGELOG.md --name-only; git diff --cached -- .claude/settings.json .github/workflows/build.yml Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs docs/features/native-skin-fixes.md CHANGELOG.md --name-only' in C:\Users\mikew\source\repos\TAOM
 succeeded in 353ms:
f63c4292b8b4c6ac869c8b6f766d2b72a1782b12
03edacc560bcec8df737eb22cc2290ec34db2b78
03edacc560bcec8df737eb22cc2290ec34db2b78

2026-06-18T20:01:04.443419Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
False

 exited 1 in 478ms:
False

 succeeded in 350ms:
diff --git a/.claude/settings.json b/.claude/settings.json
index d6ef4a0a..3459de46 100644
--- a/.claude/settings.json
+++ b/.claude/settings.json
@@ -49,6 +49,10 @@
           {
             "type": "command",
             "command": ".claude/hooks/check-moduledata-validation.sh"
+          },
+          {
+            "type": "command",
+            "command": ".claude/hooks/check-native-dll-crt.sh"
           }
         ]
       },
diff --git a/.github/workflows/build.yml b/.github/workflows/build.yml
index 30e64f1a..9c900839 100644
--- a/.github/workflows/build.yml
+++ b/.github/workflows/build.yml
@@ -74,6 +74,28 @@ jobs:
           if ($errors -gt 0) { exit 1 }
           Write-Host "All JSON files are well-formed."
 
+      - name: Validate native DLL links a static CRT
+        shell: bash
+        run: |
+          # The vendored TAOM.NativeSkinFixes.dll must link a STATIC CRT
+          # (Debug /MTd or Release /MT). A dynamic CRT (/MDd or /MD) imports
+          # vcruntime*/msvcp140*/ucrtbase*/api-ms-win-crt* — DLLs players
+          # without Visual Studio lack — so LoadLibrary fails with Win32
+          # error 126 and NativeSkinFixes goes inert. pe_inspect.py is pure
+          # stdlib and parses the PE import table cross-platform.
+          DLL="Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll"
+          if [ ! -f "$DLL" ]; then
+            echo "::notice::$DLL not present — skipping static-CRT check."
+            exit 0
+          fi
+          IMPORTS=$(python3 tools/pe_inspect.py "$DLL")
+          echo "$IMPORTS"
+          if echo "$IMPORTS" | grep -iqE 'VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt'; then
+            echo "::error::$DLL links a DYNAMIC CRT (imports above). Players without Visual Studio get LoadLibrary error 126. Rebuild with a static CRT — Debug /MTd or Release /MT — via Dependencies/NativeSkinFixes.NativeHooks/Build.ps1."
+            exit 1
+          fi
+          echo "Native DLL links a static CRT — no redistributable dependency."
+
   build:
     name: Build & Test
     runs-on: windows-latest
diff --git a/CHANGELOG.md b/CHANGELOG.md
index 8bad7cae..0277dff9 100644
--- a/CHANGELOG.md
+++ b/CHANGELOG.md
@@ -2,7 +2,95 @@
 
 ## 2026-06-18
 
-### chore(logging): gate ArmyTargeting + TroopWeight per-tick diagnostics to fire once
+### feat(settlement-food): fix garrison food starvation + tunable settlement food (TaomSettlementFoodModel)
+
+Settlements ran chronic food deficits. Root cause: the Troop Weight feature (`Patch17_TroopWeight`)
+postfixes the global `PartyBase.NumberOfAllMembers` getter and raises it to a *weighted* count, and
+vanilla `DefaultSettlementFoodModel` reads exactly that getter for the garrison food term
+(`NumberOfAllMembers / 20`). Elite garrisons (troop weights 2.0–3.0) therefore consumed 2–3× the food
+the engine intends — a globally-weighted getter leaking into an unrelated gameplay consumer (the food
+model), the same bug-class as the earlier phantom-wounded UI leak.
+
+New `TaomSettlementFoodModel : DefaultSettlementFoodModel` (food-model-only fix — the global getter
+stays weighted, so AI strength reads and garrison capacity are unchanged):
+
+- **Garrison correction:** since vanilla `NumberOfAllMembers == MemberRoster.TotalManCount`, the model
+  adds back `(weighted − raw) / garrisonDivisor` so the garrison term uses the raw body count. No-op
+  when Troop Weight is off (weighted == raw). Applies under siege too (the inflation is version-agnostic).
+- **Tunable knobs** (`settlement_food/settlement_food_config.json`, ships at vanilla values): garrison
+  + prosperity consumption divisors, town/castle base food, per-village multiplier, flat bonus, storage
+  caps. Production knobs are siege-gated (vanilla zeroes production under siege). Validated by
+  `SettlementFoodConfigProvider` (divisors ≥ 1; floats finite ≥ 0 via `FiniteFloatValidator`; invalid →
+  vanilla default + warning). MCM "Settlement Food → Enable Settlement Food Tuning" (on by default;
+  off = vanilla engine math).
+
+Thin model → pure `SettlementFoodService` (delta math, 27 unit tests) → `TownFoodSnapshot` boundary
+(ADR-002/007). Deep review: PASS (standards, 12/12 API compat, data-flow clean — 0 gaps). Reference
+doc `docs/reference/engine/settlement-economy-food-prosperity.md`; feature doc `docs/features/settlement-food.md`.
+
+### fix(native-skin-fixes): ship a static-CRT DLL so it loads for players (Win32 error 126)
+
+A player's log showed `TAOM.NativeSkinFixes.dll failed to load — feature inert. LoadLibrary failed
+(Win32 error 126)`. Error 126 (`ERROR_MOD_NOT_FOUND`) means a module in the DLL's dependency chain is
+missing. `python tools/pe_inspect.py` on the vendored DLL proved the cause: it imported `MSVCP140D.dll`,
+`VCRUNTIME140D.dll`, `VCRUNTIME140_1D.dll`, `ucrtbased.dll` — the **debug CRT**, which is not
+redistributable and ships only with Visual Studio. The DLL was a Debug build, and the vcxproj Debug
+config had no `<RuntimeLibrary>` element, so MSBuild defaulted it to `/MDd` (dynamic debug CRT). Every
+player without VS hit 126; installing the VC++ redist would not have helped (it carries the release CRT,
+not the debug CRT). `MinHook.x64.dll` was never the culprit — it imports only `KERNEL32.dll`.
+
+TAOM ships the Debug build, so the fix makes Debug self-contained: the vcxproj Debug config now sets
+`<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>` (`/MTd`, static debug CRT) to match Release's `/MT`.
+The DLL was rebuilt and re-vendored; `pe_inspect` confirms it now imports only `MinHook.x64.dll`,
+`KERNEL32.dll`, and the OS-guaranteed `SHELL32.dll`/`ole32.dll` — no redistributable dependency. Three
+guards prevent the regression from recurring: `Build.ps1` runs `pe_inspect` on its own output and throws
+on any dynamic-CRT import; the new `check-native-dll-crt.sh` PreToolUse hook blocks a commit that stages a
+dynamic-CRT DLL; the `validate-xml` CI job re-checks the committed binary. The C# loader's error-126
+message now reports whether the plugin + `MinHook.x64.dll` are present and points at the static-CRT
+requirement. Documented in `docs/features/native-skin-fixes.md` "Build & CRT requirement". The byte-pattern
+signatures remain `<PATTERN_TBD>` placeholders (separate open follow-up) — this fix makes the DLL loadable,
+a prerequisite for the hooks doing anything once patterns are authored. NativeSkinFixes tests 10/10 green;
+the 9 failing `VolunteerRecruitmentServiceTests` (Dol Guldur) are pre-existing and unrelated.
+
+### feat(shader-precompilation): auto-skip scenes that hard-crash the walk (per-scene crash guard)
+
+A user's mods-removed run crashed the walk at item 9 (`taom_rohan_battle_fords_of_isen_forceatmo`) with a
+pure-native ACCESS_VIOLATION during the scene's `MissionInitialize`, concurrent with the `pbr_terrain`
+input-layout-9 shader compile (the same shader that division-by-zeros at Helm's Deep) — GPU/driver-specific
+(the scene loads fine on other machines). A native scene-load crash isn't catchable in managed code, so it
+hard-stops the walk; without a guard, an affected user can never get past that item. (Diagnosed from the
+user's `taom_debug` + `rgl_log` + `palantir` triple, reconciled by timestamp; the popup-spam in their first
+report was a *pre-existing* third-party MBSuperSpeed `get_InputManager` AV, not TAOM.)
+
+New `ShaderPrecompileCrashGuard` (mirrors `BattleLoadStallMarker`): the runner writes the scene id it is
+about to load to `Logs/shader-precompile-inflight.marker` (survives a hard crash); if that marker is still
+there at the next walk's start, the scene crashed mid-load and is recorded to a persistent
+`Logs/shader-precompile-crashed-scenes.txt` skip list, which the runner drops from the plan so the walk
+completes. ScenePass items only (the character battle is essential); the marker is cleared only at full
+item resolution (load + compile + teardown), so a slow item or a clean exit never gets recorded — only a
+true process crash does. Delete the crashed-scenes file to retry. 10 new unit tests (34 total); build 0/0.
+**Known limitation:** the underlying `pbr_terrain` input-layout-9 div-by-zero (Helm's Deep + fords_of_isen)
+is a vanilla engine shader bug; the root fix (a shader-source override) is deferred — the guard makes the
+walk robust to it (and any other GPU-specific bad scene) in the meantime. Reviewed via `/deep-review --codex`
+— 5 agents + Codex `gpt-5.5 xhigh` both clean (Codex SHIP 0/0/0/0, all 7 Known Suspects DISPUTED with
+file:line evidence; the load-bearing false-skip trace confirmed every non-crash item-end clears the marker
+via the `TickEnding` resolution block). The data-flow agent caught one LOW Codex missed: `OnItemFailed`
+only checked `generation`, not `_state`, so a late failure callback could re-enter `BeginEnd` and reset the
+Ending timer — fixed by mirroring `OnItemRendering`'s `generation + _state==Starting` stale-callback guard.
+
+### fix(shader-precompilation): suppress the battle-load stall watchdog during the walk
+
+A player's cold-cache run compiled item 1 (the all-troops character battle, 3000 troops) for **830s**;
+the `BattleLoadStallWatchdog` (300s threshold) false-positived at 305s and emitted a spurious
+`[CrashReport]` bundle mid-walk. The load wasn't stuck — it finished at 830s and the walk completed —
+but the user got an alarming "crash" artifact. This hits every first-time cold-cache run, since item 1
+is always the longest. Fix: `ShaderPrecompileRunner` sets `BattleLoadStallWatchdog.SuppressStallDetection`
+(new `volatile` static) true for the whole walk (in `Begin`) and clears it in `Finish`; the watchdog's
+`Poll` early-returns while suppressed. The precompile's long loads are intentional and the runner has its
+own per-item timeouts, so the stall watchdog (which exists to catch *real* battle hangs) should stay
+quiet during the walk. Diagnosed from a player's `rgl_log` + `palantir` + `taom_debug` triple
+(the popup error they reported was actually a pre-existing third-party MBSuperSpeed `get_InputManager`
+AV — 13k occurrences starting 18h before the precompile — not TAOM). Build 0/0.
 
 Both features are confirmed working, so their per-event diagnostics (`ArmyTargeting:` border-floor /
 strength / target / distance-compensation DEBUG lines; `[TroopWeight][diag] Shed` INFO line) no longer
diff --git a/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1 b/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
index a77cfce0..20ef95a0 100644
--- a/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
+++ b/Dependencies/NativeSkinFixes.NativeHooks/Build.ps1
@@ -55,6 +55,26 @@ $dllPath = Join-Path $outDir 'TAOM.NativeSkinFixes.dll'
 if (-not (Test-Path $dllPath)) {
     throw "Build succeeded but output DLL missing: $dllPath"
 }
+
+# Static-CRT guard. The shipped DLL must link the CRT statically (Debug=/MTd,
+# Release=/MT). A dynamic CRT (/MDd or /MD) imports vcruntime*/msvcp140*/
+# ucrtbase*/api-ms-win-crt* — DLLs players without Visual Studio lack — and
+# LoadLibrary then fails with error 126. Reject the bad binary before it can be
+# vendored. See docs/features/native-skin-fixes.md "Build & CRT requirement".
+$peInspect = Join-Path $scriptDir '..\..\tools\pe_inspect.py'
+$py = Get-Command python3 -ErrorAction SilentlyContinue
+if (-not $py) { $py = Get-Command python -ErrorAction SilentlyContinue }
+if ($py -and (Test-Path $peInspect)) {
+    $imports = & $py.Source $peInspect $dllPath 2>&1 | Out-String
+    if ($imports -match '(?i)VCRUNTIME|MSVCP[0-9]+|MSVCR[0-9]+|ucrtbase|api-ms-win-crt') {
+        Write-Host $imports
+        throw "[NativeSkinFixes] BUILD REJECTED: $dllPath links a DYNAMIC CRT (see imports above). Rebuild with a static CRT (Debug=/MTd, Release=/MT). The dynamic/debug CRT is absent on players' machines -> LoadLibrary error 126."
+    }
+    Write-Host "[NativeSkinFixes] CRT check OK -> static CRT, no redistributable dependency." -ForegroundColor Green
+} else {
+    Write-Host "[NativeSkinFixes] WARNING: python or tools/pe_inspect.py not found; skipped static-CRT import check (the commit hook + CI still gate it)." -ForegroundColor Yellow
+}
+
 $size = (Get-Item $dllPath).Length
 Write-Host "[NativeSkinFixes] OK -> $dllPath ($size bytes)" -ForegroundColor Green
 Write-Host "[NativeSkinFixes] Run './build.ps1' to repackage TAOM.dll + redeploy."
diff --git a/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj b/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
index 9062ab10..805c9328 100644
--- a/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
+++ b/Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj
@@ -62,6 +62,12 @@
       <PrecompiledHeaderFile>pch.h</PrecompiledHeaderFile>
       <LanguageStandard>stdcpp17</LanguageStandard>
       <AdditionalIncludeDirectories>$(ProjectDir)MinHook\include;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
+      <!-- Static debug CRT (/MTd): link the CRT into the DLL so it has NO external
+           ucrtbased.dll / vcruntime140d.dll dependency. Without this, MSBuild
+           defaults Debug to /MDd (dynamic debug CRT), and the debug CRT is NOT
+           redistributable — players without Visual Studio get LoadLibrary error
+           126. TAOM ships the Debug build, so this MUST be static. -->
+      <RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>
       <ExceptionHandling>Async</ExceptionHandling>
     </ClCompile>
     <Link>
diff --git a/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs b/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
index ec0e28f6..15c44160 100644
--- a/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
+++ b/Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs
@@ -81,7 +81,17 @@ internal static class NativeHookLoader
         if (_hooksModule == IntPtr.Zero)
         {
             int err = Marshal.GetLastWin32Error();
-            _lastLoadError = $"LoadLibrary failed (Win32 error {err}). Expected DLL at: {Path.Combine(binDir, DllName + ".dll")}";
+            string pluginPath = Path.Combine(binDir, DllName + ".dll");
+            _lastLoadError = $"LoadLibrary failed (Win32 error {err}). Expected DLL at: {pluginPath}";
+            if (err == 126) // ERROR_MOD_NOT_FOUND: the plugin OR one of its dependency DLLs is missing.
+            {
+                bool pluginPresent = File.Exists(pluginPath);
+                bool minHookPresent = File.Exists(Path.Combine(binDir, "MinHook.x64.dll"));
+                _lastLoadError += $" (error 126 = a module in the dependency chain is missing." +
+                    $" plugin present: {pluginPresent}, MinHook.x64.dll present: {minHookPresent}." +
+                    " If both are present, the plugin was likely built against a non-static CRT — a debug/dynamic" +
+                    " build needs Visual Studio's runtime DLLs that players don't have. Rebuild static: Debug /MTd or Release /MT.)";
+            }
             return false;
         }
 
diff --git a/docs/features/native-skin-fixes.md b/docs/features/native-skin-fixes.md
index 59bc634a..86ca5661 100644
--- a/docs/features/native-skin-fixes.md
+++ b/docs/features/native-skin-fixes.md
@@ -150,6 +150,50 @@ to fill them in.
   Each hook reports success / failure individually so partial degradation is
   visible.
 
+## Build & CRT requirement (static CRT is mandatory)
+
+The native DLL **must link the C runtime statically.** This is the line between
+"loads on every player's machine" and "fails with `LoadLibrary` Win32 error 126
+for anyone without Visual Studio."
+
+- **TAOM ships the Debug build** (built from Visual Studio). The vcxproj Debug
+  config therefore sets `<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>`
+  (`/MTd`, **static** debug CRT); Release sets `MultiThreaded` (`/MT`, static).
+  Either is self-contained — never vendor a dynamic-CRT build.
+- **Why it matters:** a dynamic CRT (`/MDd` debug, or `/MD` release) makes the
+  DLL import `vcruntime140*.dll` / `msvcp140*.dll` / `ucrtbase*.dll`. The
+  **debug** CRT (`*140d.dll`, `ucrtbased.dll`) is **not redistributable** — it
+  ships only with Visual Studio — so a Debug `/MDd` build loads on a dev machine
+  but errors 126 for every player. Installing the VC++ redist does NOT help: it
+  contains the *release* CRT, not the debug CRT. MSBuild's Debug default with no
+  explicit `<RuntimeLibrary>` is `/MDd` — that exact gap shipped a Debug DLL that
+  failed for players (2026-06-18).
+- **A correct static-CRT build imports only `MinHook.x64.dll` + `KERNEL32.dll`.**
+  Verify any rebuild with `python tools/pe_inspect.py
+  Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` — any
+  `VCRUNTIME*` / `MSVCP140*` / `ucrtbase*` / `api-ms-win-crt*` import means the
+  build is dynamic and must be redone.
+- **`MinHook.x64.dll` is a required sidecar.** It is dynamically linked, but its
+  own only import is `KERNEL32.dll`, so it is itself self-contained. It must sit
+  next to the plugin in `bin/Win64_Shipping_Client/`. The `.gitignore` allowlist
+  force-includes both DLLs and the vcxproj `CopyMinHookSidecar` post-build target
+  keeps it in sync.
+
+**Three automated guards enforce this** (none replaces an in-game load test, but
+they stop the regression at the source):
+
+1. `Build.ps1` runs `pe_inspect.py` on its own output and `throw`s on any
+   dynamic-CRT import before the DLL can be vendored.
+2. The `check-native-dll-crt.sh` PreToolUse hook blocks a `git commit` that
+   stages a dynamic-CRT DLL.
+3. The `validate-xml` CI job (`.github/workflows/build.yml`) re-runs the same
+   check on the committed binary.
+
+> The byte-pattern signatures still ship as `<PATTERN_TBD>` placeholders (see
+> "Open follow-ups"), so even once the DLL loads, the hooks stay inert until the
+> patterns are authored. The static-CRT fix is a **prerequisite** — it makes the
+> DLL *loadable* on player machines — not the feature's activation.
+
 ## Tests
 
 - `TAOM.Tests/Features/NativeSkinFixes/NativeSkinFixesInstallerTests.cs` — 8
warning: in the working copy of 'Dependencies/NativeSkinFixes.NativeHooks/Build.ps1', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Dependencies/NativeSkinFixes.NativeHooks/NativeSkinFixes.NativeHooks.vcxproj', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/Features/NativeSkinFixes/Interop/NativeHookLoader.cs', LF will be replaced by CRLF the next time Git touches it

 succeeded in 574ms:

[32;1mMode [0m[32;1m Length[0m[32;1m Name[0m
[32;1m---- [0m [32;1m------[0m [32;1m----[0m
d----        bl-harmony-decompiled
d----        claude
d----        harmony-decompiled
d----        uiextenderex_src
d----        uiextenderex-decompiled
-a--- 3351   AbilityCatalogService.cs
-a--- 4433   common_lords.txt
-a--- 20875  CraftingPieceCatalogService.cs
-a--- 26970  CrossReferenceService.cs
-a--- 17710  FactionCatalogService.cs
-a--- 8230   final_report.txt
-a--- 5727   find_common_lords.ps1
-a--- 866    find_missing_lords.ps1
-a--- 17101  gen_remaining_careers.py
-a--- 4226   generate_lord_templates.ps1
-a--- 44313  is_female_template.xslt
-a--- 7769   ItemCatalogService.cs
-a--- 3703   ItemTraitCatalogService.cs
-a--- 2643   IValidationService.cs
-a--- 8366   lotraom_lords.txt


codex
The object hashes confirm the working-tree DLL differs from both the index and HEAD (`f63c...` vs `03ed...`). I’m going to parse the HEAD/index binary directly from `git show` in memory now, because that determines what CI would validate if a commit were made without staging the DLL.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
import subprocess, struct

def u(d, off, fmt): return struct.unpack_from(fmt, d, off)
def rva_to_off(rva, sections):
    for vaddr, vsize, roff, rsize in sections:
        if vaddr <= rva < vaddr + max(vsize, rsize):
            return roff + (rva - vaddr)
    return None
def cstr(d, off):
    end = d.index(b'\\x00', off)
    return d[off:end].decode('ascii', 'replace')
def imports_for(spec):
    d = subprocess.check_output(['git','show',spec])
    e_lfanew = u(d, 0x3C, '<I')[0]
    coff = e_lfanew + 4
    machine, nsec = u(d, coff, '<HH')
    opt_size = u(d, coff + 16, '<H')[0]
    opt = coff + 20
    magic = u(d, opt, '<H')[0]
    dd_off = opt + (112 if magic == 0x20b else 96)
    import_rva = u(d, dd_off + 8, '<I')[0]
    sec_off = opt + opt_size
    sections=[]
    for i in range(nsec):
        vsize, vaddr, rsize, roff = u(d, sec_off + i*40 + 8, '<IIII')
        sections.append((vaddr,vsize,roff,rsize))
    imps=[]
    if import_rva:
        io = rva_to_off(import_rva, sections)
        while io is not None:
            nm_rva = u(d, io + 0x0C, '<I')[0]
            if nm_rva == 0: break
            imps.append(cstr(d, rva_to_off(nm_rva, sections)))
            io += 0x14
    print(spec)
    print(', '.join(imps))

path='Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll'
for spec in ['HEAD:'+path, ':'+path]:
    imports_for(spec)
'@ | python -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 493ms:
HEAD:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll
MinHook.x64.dll, KERNEL32.dll, MSVCP140D.dll, VCRUNTIME140D.dll, VCRUNTIME140_1D.dll, ucrtbased.dll
:Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll
MinHook.x64.dll, KERNEL32.dll, MSVCP140D.dll, VCRUNTIME140D.dll, VCRUNTIME140_1D.dll, ucrtbased.dll

codex
**Findings**

[HIGH] Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll:0 — Release artifact — The working-tree DLL is static, but the staged/index DLL is still the old dynamic CRT binary. I parsed the index/HEAD blob in-memory: `MinHook.x64.dll, KERNEL32.dll, MSVCP140D.dll, VCRUNTIME140D.dll, VCRUNTIME140_1D.dll, ucrtbased.dll`. The hook only checks `git diff --cached --name-only` and exact DLL staging: `.claude/hooks/check-native-dll-crt.sh:50` `STAGED=$(git diff --cached --name-only...)`, line 60 `[[ "$f" == "$DLL" ]]`, line 62 exits `{}` when absent. Fix: stage the rebuilt DLL and all guard files before commit; validate the staged blob, not just the working tree.

[LOW] .claude/hooks/check-native-dll-crt.sh:37 — Git matcher — The direct reject only covers contiguous `git commit-`: `*"git commit-"*) echo '{}'; exit 0 ;;`. But line 40 accepts option-prefixed forms: `*"git -"*" commit"*`, so `git -C . commit-tree ...` / `git -c k=v commit-graph ...` are wrongly treated as commits. Fix: make the matcher token-aware after Git options, rejecting `commit-*` before accepting exact `commit`.

[LOW] docs/features/native-skin-fixes.md:171 — Documentation — The doc says a static build imports only `MinHook.x64.dll + KERNEL32.dll`, but the verified current DLL imports `MinHook.x64.dll, KERNEL32.dll, SHELL32.dll, ole32.dll`. The changelog is correct. Fix: update the doc and hook comment to include OS-guaranteed `SHELL32.dll` / `ole32.dll`.

**Known Suspects**

1. CONFIRMED — no issue. Internal hook failures fail open: `cd` failure line 44 emits `{}`, missing DLL/Python/tool lines 65-68 emit `{}`, empty output line 71 emits `{}`, JSON escape failure line 93 emits `{}`. Only the final deny path line 95 blocks, and exits 0.

2. DISPUTED — LOW above. Listed normal forms match, and direct `git commit-tree` / `git commit-graph` are rejected, but option-prefixed plumbing commands are false positives.

3. CONFIRMED with caveat. Exact staged path scope is line 46 plus line 60. The hook checks on-disk DLL via line 70 `tools/pe_inspect.py "$DLL"`, not the staged blob. That is a real local gap, but CI catches the committed blob if the CI step is committed and enforced.

4. CONFIRMED — no issue. `Build.ps1` uses 5.1-safe constructs: `#requires -Version 5.1` line 1, `Get-Command` lines 65-66, native invocation line 68, `-match` line 69, `throw` line 71. No PS7-only syntax.

5. CONFIRMED — no issue in code. The regex at Build.ps1:69, hook:76, CI:93 catches v143 debug/release CRT names and future `MSVCP150`-style names. It does not match the current static imports.

6. CONFIRMED — no issue. `[0-9]+`, `|`, and case-insensitivity are valid for .NET regex and POSIX ERE with `grep -iqE`; avoiding `\d` was the right portability choice.

**Required Analysis**

A. Remaining dynamic-CRT paths, ranked: current staging omission is highest risk; bad DLL staged then swapped to good on disk is caught by CI but missed locally; missing Python/pe_inspect fails open locally but CI catches; vcxproj reverted to dynamic is caught by Build.ps1 if Python works and by CI regardless; future `MSVCP150` is caught; manual release/CI bypass remains outside these guards.

B. `pe_inspect.py` does not truncate imports: `--max-names` only limits exports at tools/pe_inspect.py:59 and :64; imports are fully joined at :77. If a future tool change stopped printing imports while still exiting successfully, the grep-only guards would silently pass.

C. `NativeHookLoader.cs` is safe: `System.IO` is imported at line 2, `File.Exists` is only used to append diagnostics at lines 88-93, and control flow still returns false at line 95. The message accurately distinguishes plugin/MinHook presence from likely CRT/dependency failure.

D. `MultiThreadedDebug` is the correct `/MTd` token and is scoped to Debug at vcxproj:55-70; Release remains `/MT` at :81-93. MinHook stays a sidecar import via :77-78 and :134. Static CRT is acceptable here because the exported surface is C ABI bool/void/uintptr_t, with no CRT-owned allocations crossing the boundary.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 0 | LOW: 2  
VERDICT: NEEDS FIXES
SUCCESS: The process with PID 64408 (child process of PID 61732) has been terminated.
SUCCESS: The process with PID 61732 (child process of PID 26292) has been terminated.
SUCCESS: The process with PID 26292 (child process of PID 69456) has been terminated.
SUCCESS: The process with PID 69456 (child process of PID 30424) has been terminated.
SUCCESS: The process with PID 30424 (child process of PID 67924) has been terminated.
SUCCESS: The process with PID 32032 (child process of PID 41280) has been terminated.
SUCCESS: The process with PID 41280 (child process of PID 50468) has been terminated.
SUCCESS: The process with PID 50468 (child process of PID 58120) has been terminated.
SUCCESS: The process with PID 58120 (child process of PID 67924) has been terminated.
tokens used
112,903
**Findings**

[HIGH] Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll:0 — Release artifact — The working-tree DLL is static, but the staged/index DLL is still the old dynamic CRT binary. I parsed the index/HEAD blob in-memory: `MinHook.x64.dll, KERNEL32.dll, MSVCP140D.dll, VCRUNTIME140D.dll, VCRUNTIME140_1D.dll, ucrtbased.dll`. The hook only checks `git diff --cached --name-only` and exact DLL staging: `.claude/hooks/check-native-dll-crt.sh:50` `STAGED=$(git diff --cached --name-only...)`, line 60 `[[ "$f" == "$DLL" ]]`, line 62 exits `{}` when absent. Fix: stage the rebuilt DLL and all guard files before commit; validate the staged blob, not just the working tree.

[LOW] .claude/hooks/check-native-dll-crt.sh:37 — Git matcher — The direct reject only covers contiguous `git commit-`: `*"git commit-"*) echo '{}'; exit 0 ;;`. But line 40 accepts option-prefixed forms: `*"git -"*" commit"*`, so `git -C . commit-tree ...` / `git -c k=v commit-graph ...` are wrongly treated as commits. Fix: make the matcher token-aware after Git options, rejecting `commit-*` before accepting exact `commit`.

[LOW] docs/features/native-skin-fixes.md:171 — Documentation — The doc says a static build imports only `MinHook.x64.dll + KERNEL32.dll`, but the verified current DLL imports `MinHook.x64.dll, KERNEL32.dll, SHELL32.dll, ole32.dll`. The changelog is correct. Fix: update the doc and hook comment to include OS-guaranteed `SHELL32.dll` / `ole32.dll`.

**Known Suspects**

1. CONFIRMED — no issue. Internal hook failures fail open: `cd` failure line 44 emits `{}`, missing DLL/Python/tool lines 65-68 emit `{}`, empty output line 71 emits `{}`, JSON escape failure line 93 emits `{}`. Only the final deny path line 95 blocks, and exits 0.

2. DISPUTED — LOW above. Listed normal forms match, and direct `git commit-tree` / `git commit-graph` are rejected, but option-prefixed plumbing commands are false positives.

3. CONFIRMED with caveat. Exact staged path scope is line 46 plus line 60. The hook checks on-disk DLL via line 70 `tools/pe_inspect.py "$DLL"`, not the staged blob. That is a real local gap, but CI catches the committed blob if the CI step is committed and enforced.

4. CONFIRMED — no issue. `Build.ps1` uses 5.1-safe constructs: `#requires -Version 5.1` line 1, `Get-Command` lines 65-66, native invocation line 68, `-match` line 69, `throw` line 71. No PS7-only syntax.

5. CONFIRMED — no issue in code. The regex at Build.ps1:69, hook:76, CI:93 catches v143 debug/release CRT names and future `MSVCP150`-style names. It does not match the current static imports.

6. CONFIRMED — no issue. `[0-9]+`, `|`, and case-insensitivity are valid for .NET regex and POSIX ERE with `grep -iqE`; avoiding `\d` was the right portability choice.

**Required Analysis**

A. Remaining dynamic-CRT paths, ranked: current staging omission is highest risk; bad DLL staged then swapped to good on disk is caught by CI but missed locally; missing Python/pe_inspect fails open locally but CI catches; vcxproj reverted to dynamic is caught by Build.ps1 if Python works and by CI regardless; future `MSVCP150` is caught; manual release/CI bypass remains outside these guards.

B. `pe_inspect.py` does not truncate imports: `--max-names` only limits exports at tools/pe_inspect.py:59 and :64; imports are fully joined at :77. If a future tool change stopped printing imports while still exiting successfully, the grep-only guards would silently pass.

C. `NativeHookLoader.cs` is safe: `System.IO` is imported at line 2, `File.Exists` is only used to append diagnostics at lines 88-93, and control flow still returns false at line 95. The message accurately distinguishes plugin/MinHook presence from likely CRT/dependency failure.

D. `MultiThreadedDebug` is the correct `/MTd` token and is scoped to Debug at vcxproj:55-70; Release remains `/MT` at :81-93. MinHook stays a sidecar import via :77-78 and :134. Static CRT is acceptable here because the exported surface is C ABI bool/void/uintptr_t, with no CRT-owned allocations crossing the boundary.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 0 | LOW: 2  
VERDICT: NEEDS FIXES
