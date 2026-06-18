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
