# Cold review: plan 006 (crash-capture boot cost)

Reviewer: cold read, no prior context. Plan: `plans/006-crash-capture-boot-cost.md` (1,094 lines).
Template: `.claude/skills/improve/references/plan-template.md` ("Quality bar").
Code compared at `b2e387db` with `git show b2e387db:<path>`; engine facts against the v1.5.3
decompile cache `C:\Users\mikew\.taom-src\v1.5.3\`; MCM claim against a fresh `ilspycmd` decompile of
the installed `Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\MCMv5.dll`. The drift check (plan
line 10) re-run at HEAD `4b5662b2` printed nothing.

**Verdict:** the technical content is unusually well-grounded. Every excerpt that carries weight
matches the code, every engine name and signature matches the decompile, and the TDD order is right
for every behaviour change. One blocking defect: where the executor works. It sits in the harness,
not in the code, and costs a paragraph to fix. The dash gate in Done criteria can never fail on this
machine. It should be fixed, but it does not stop execution.

## Blocking

**B1. Nothing pins the executor to the worktree. The fallback is the shared main checkout, which
holds another session's uncommitted edits (lines 440-441, 492-493, 506, 454-473).**
Scope and every step use repo-relative paths (`Main/SubModule.cs`, `TAOM.Tests/...`). The only
anchor is "Create the worktree ... and `cd` into it" (line 506) and "Run every command from your
worktree root" (line 440). In this harness a subagent's shell cwd resets to `E:\repos\TAOM` on every
Bash call, and the Read and Edit tools need absolute paths. So a weak executor builds each path from
its primary directory, `E:\repos\TAOM`. The main tree at this moment has uncommitted edits to
`Main/SubModule.cs` (hunks at 1457 and 1970, per `git diff -U0`), `Main/IoC.cs` and `CHANGELOG.md`.
A wrong-tree Step 8 edit followed by `git add Main/SubModule.cs` (line 835) would commit the other
session's SubModule hunks onto `bannerlord-1.5.x`. That breaks AGENTS.md "Preserve others' work".
The plan itself warns this tree is unsafe (lines 46-48, 481-483) but never gives the executor a
mechanical guard. **Fix:** add under "Commands you will need":
"Every shell call starts with `cd E:/repos/wt-006-crash-capture &&`, and every git call uses
`git -C E:/repos/wt-006-crash-capture`. Every Read or Edit path is absolute under
`E:\repos\wt-006-crash-capture\`. Never read or write a path under `E:\repos\TAOM\` except the drift
check." Also add a pre-commit check with an exact expected result:
`git -C E:/repos/wt-006-crash-capture rev-parse --abbrev-ref HEAD` prints
`plan-006-crash-capture-boot-cost`. Add a STOP condition: "any file you changed resolves under
`E:\repos\TAOM\`". If the orchestrator supplies its own worktree (line 493), have it substitute that
absolute root.

## Non-blocking

1. **The dash check in Done criteria is vacuous on this machine (line 1032; relied on at 426,
   904, 968).** Python here is 3.14.0, and with `PYTHONUTF8` unset piped stdin decodes as cp1252.
   A UTF-8 em dash (`E2 80 94`) then decodes as "â€”", which never equals `chr(0x2014)`. I proved
   it: `printf '+a \xe2\x80\x94 b\n+c \xe2\x80\x93 d\n' | python -c "<the plan's one-liner>"` printed
   nothing and exited 0. The same input through `python -X utf8 -c ...` finds the line. **Fix:**
   use `python -X utf8 -c ...`, or read `sys.stdin.buffer.read().decode('utf-8')`. The text the plan
   supplies has no dashes outside code excerpts (I scanned every line), so today this gate only
   guards the executor's own lines: the `Resolve` body, the tests and any comment it rewraps.
2. **The shell is never named.** Several commands only work in Git Bash: `sed -n 194,210p` (line
   507), `grep -c` (line 1038), and `git grep -n "EndsWith(\"CallbacksGenerated\"" -- Main` (lines
   717, 1024). In PowerShell, `\"` does not escape and ends the string. The primary shell here is
   PowerShell. Add one line: "Run every command in this plan in Git Bash (the Bash tool)."
3. **Step 8's expected diff size is wrong (line 833-834).** I applied the exact Step 8 replacement
   to `git show b2e387db:Main/SubModule.cs` and diffed it: `13 insertions(+), 14 deletions(-)`, not
   "about 6 lines added, 10 removed". A literal executor may STOP on a correct edit. Use "13 added,
   14 removed" or drop the numbers and keep the hunk-range check. With `-U0` the change shows as two
   hunks (`@@ -195 +195,6` and `@@ -197,13 +202,7`). The Done criterion at line 1035 says "one
   hunk", which holds only at the default `-U3`. Say "with default context".
4. **The `Resolve` body is a comment, not code (lines 683-686).** Pasted as written it fails with
   CS0161. The prose says what to write, but this plan is otherwise exact, and the body is about ten
   lines. Give it in full, or add "implement the body described in the comment".
5. **The baseline is pinned to the live Armory's current state (lines 431-436, 514-515,
   1019-1020).** `plans/_audit/2026-09-23-opus/baseline.md` confirms it: 10,239 total, 10,235 passed,
   2 failed, 2 not executed. Both failures read the unversioned Armory that another session is
   editing. If that session fixes or breaks it, "exactly 2 failures" fails, and line 436 ("Treat any
   OTHER failure as yours") invites repair outside scope. Say "at most these 2 Armory failures,
   plus any other failure present in Step 0's baseline run; record the Step 0 list". Step 0 also has
   no instruction for a baseline that differs.
6. **Step 1 lands a C# change with no test first (lines 517-530).** The `Not-tested:` trailer
   justifies it (timing appears only in a game launch), but AGENTS.md says "TDD, test first, always".
   Keep it and add one line naming the exemption, or fold the stopwatch into Step 5, which already
   rewrites the same log line (line 702).
7. **Plan 009 conflict is not mentioned (Maintenance notes, 1063-1093; "Depends on", line 22).**
   `plans/009-guarded-patch-category-apply.md` is cut from the same `b2e387db` and also edits the
   Patch37 site at `SubModule.cs:199` (its line 119, action A) and `docs/features/crash-report.md`.
   Its line 237 argues from the `MBSubModuleBase.OnSubModuleLoad` finalizer at
   `Patch37_CrashReport.cs:113`, which this plan deletes. Add a 009 bullet next to the 007 one.
8. **Some doc lines go stale and are not in Step 11.** `docs/features/crash-report.md:207` still
   names `TaleWorlds.MountAndBlade.View` as needed for the `MissionView.OnMissionScreenTick` target.
   `:213` says "88 tests (counted 2026-09-22)". `harmony-patch-registry.md:270` still says Patch37
   "maximise[s] coverage of other mods' OnSubModuleLoad throws". Line 1089-1093 defers only the
   `UnpatchCategory` half of 270. Either add these, or list them under "Deferred".
9. **`reflection-sites.md` category (line 957).** The new row sits in Category D ("TAOM-internal
   reflection ... intentionally not gated", file lines 103-105), but it now names six engine members
   looked up by string. The row does say they are pinned by `Native2ManagedTargetsTests`, so this is
   defensible. A reviewer may still ask for Category B.
10. **`lint_docs.py` "exit 0" proves little (lines 449, 968, 1033).** With no flags the script
    returns 0 whatever it finds (`tools/lint_docs.py:1433-1527`). The new Key Files link
    (`Native2ManagedTargets.cs`, line 930) would be checked by `--fail-on-dead`. Use
    `python tools/lint_docs.py --fail-on-dead`.
11. **Step 11.7 wording (lines 963-966).** "it must exit 0. If it reports a difference, run ...
    write mode" reads as a contradiction. Say: "Expected exit 0 after your hand edit. If it exits
    1, run write mode and check that the diff ...".

## Excerpt check (item 3), all at `b2e387db`

Matches, verified line by line:

- `Main/SubModule.cs:187-210` (plan 81-105), byte-exact, including the em dash in the kept comment
  at 191. `SubModule.cs:126` uses `PatchShield` (plan 319). The file is 2,148 lines.
- `Native2ManagedPatcher.cs` (123 lines): lines 9-10, 16-19, 21-26, 36-59, 72-81, 95-114 and
  117-123 as quoted.
- `Patch37_CrashReport.cs` (120 lines): header 11-27; the four dead classes at 45-52, 63-70,
  104-111 and 113-120; the five kept at 36-43, 54-61, 72-79, 86-93 and 95-102; `Category` const at 33.
- `CrashReportSettings.cs:17-32` (52 lines). The four existing `RequireRestart = false` are at
  25, 37, 44 and 49, so the Done count of 6 (line 1027) is right.
- `SettingRequireRestartPostureTests.cs` (113 lines): 23-29 and 37-42 as quoted; offenders are
  formatted `CrashReportSettings.EnableCrashCapture`, as Step 7.2 expects.
- `CrashReportService.cs:94-115`, `CrashBundleThrottle.cs:60-83` (increment at 65-66),
  `CrashReportPatchHelper.cs:36-41`, `AppDomainExceptionHook.cs:60`.
- `RethrowStackPreserver.cs:63` signature, namespace `TAOM.Dependencies.Foundation`, returns the same
  instance. `TAOM.Dependencies.dll` is in `TAOM.Tests\bin\Debug\net472\`, and six existing tests
  already use it.
- `TAOM.csproj:117-121` `InternalsVisibleTo("TAOM.Tests")`; `TAOM.Tests.csproj:35-38`.
  The three AutoGenerated DLLs are in the test bin. `TaleWorlds.MountAndBlade.View.dll` is not in the
  test bin; it is in `Modules\Native\bin\Win64_Shipping_Client\`.
- `GameAssemblies` (internal static, `EnsureLoaded()`, `Diagnostics`); `HarmonyPatchBindingTests`
  `MergeSpec` at 184-211 and `DiscoverPatchTypes` at 80-96.
- `.claude/rules/harmony-patches.md:50` quoted correctly. Harmony 2.4.2 (by reflection on the
  NuGet DLL): `HarmonyPatchCategory : HarmonyAttribute`, `.ctor(string)`, and `HarmonyMethod.category`
  is a string.
- Engine (v1.5.3 cache): the four base virtuals at the quoted lines, with empty or assert-only
  bodies. The five kept targets are non-virtual at the quoted lines, and the dev-trigger target
  `Module.OnApplicationTick` is non-virtual too, so Step 2's RED lists exactly four. All six shims
  exist as `internal static` at :406, :382, :418, :600, :850 (Engine) and :534 (Core), in namespace
  `ManagedCallbacks`.
- MCM: `BaseSettingsProvider.Instance { get; internal set; }`. Its only assignment is in
  `MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot` (decompiled `MCM/MCMSubModule.cs:73`).
- Docs: crash-report.md lines 5, 32, 34, 54-65, 69, 82, 104, 106, 190, 191, 224, 273, 277-278,
  284-285 and `## Changelog` at 301; mcm.md 93-100; hero-race.md 242-243;
  gauntletui-viewmodel-screen.md 111-112; harmony-patch-registry.md 268 and 270; patch-targets.md
  `Patches: 247.` (line 7) and rows 101, 102, 104, 108; reflection-sites.md 111;
  submodule-lifecycle-and-harmony.md:57.
- The `cost zero` grep at `b2e387db` hits exactly the five places the plan rewrites
  (`CrashReportSettings.cs:31`, `Native2ManagedPatcher.cs:16`, `crash-report.md:69,277,278`).
- No test or tool keys on the changed log strings. `RethrowStackPreserverTests.cs:241` uses
  `ModuleOnApplicationTickFinalizer`, which is kept.

Mismatches:

- Plan line 210: "each is a 7-line block". Each dead class is 8 lines (for example 45-52). Step 3's
  ranges are correct.
- Plan line 834: diff size (see non-blocking 3).
- Plan line 953: the main checkout's uncommitted hunk in reflection-sites.md inserts 3 lines after 43
  (`@@ -43,0 +44,3`), not "lines 41-47". This does not matter in the worktree.
- Not verified: the per-class static-method counts (112/86/43, line 182). Nothing depends on them;
  247 minus 6 gives the 241 at line 1066.

## Item 4 checklist

- **TDD order:** RED then GREEN for 2/3, 4/5, 6/7 and 9/10. Step 1 is the exception (non-blocking 6).
  Step 8 is a parity-preserving deletion with no test, which is acceptable.
- **Issue-first:** line 25, "create before implementation lands (orchestrator)". OK.
- **Binding ADRs:** 002, 003/004/005, 007, 008 and the harmony-patches rule, each with a one-line
  summary (lines 410-427). OK.
- **Single-owner files:** `SubModule.cs` is scoped to lines 194-210 with the exact replacement (lines
  456-457, 807-829). `IoC.cs`, `CrashReportIoC.cs` and the csproj files are out of scope, with a
  STOP (lines 477-480, 1054-1055). OK.
- **STOP conditions:** specific: the RED contents, a missing shim with the decompile command, the
  preserver identity, snapshot scope, and evidence that a dropped shim mattered. Add the wrong-tree
  guard from B1.
- **Done criteria:** machine-checkable, except the vacuous dash gate (non-blocking 1) and the
  Armory-dependent failure count (non-blocking 5).
- **Planned-at SHA and drift paths:** `b2e387db` throughout. The drift-check paths (line 10) cover
  every in-scope path (lines 456-473). OK.
- **Non-deploying commands:** build and test carry `-p:DisableModuleCopy=true -p:ModuleId=`
  (lines 445-447, 1018-1021). `./build.ps1` and launching the game are forbidden (line 441). OK.

## Item 5

- Em or en dashes in prose: none. The 9 lines that contain one (86, 130, 191, 200, 348, 388, 391,
  400, 888) are all verbatim code excerpts, which are exempt.
- Secrets: none. Paths and a public NuGet location only.
