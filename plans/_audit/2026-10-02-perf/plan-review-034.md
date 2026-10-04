# Plan review 034, round 1 (cold)

Plan: `plans/034-patchshield-per-call-cost.md` (1148 lines). Reviewed against `dffdf879` with
`git show dffdf879:<path>`. The worktree HEAD is `e9cd8b39`, two docs commits later.
`git diff --stat dffdf879..HEAD` over the plan's drift-check paths prints nothing, so none of the
in-scope files has drifted. No earlier `plan-review-034*.md` exists, so there are no earlier blocking
items to re-check.

As a read-only reviewer I did not run dotnet. The B1 characterization set (green at base) and the
B3 lookup on .NET Framework are UNVERIFIED by me. The plan covers both with STOP conditions
(lines 845-847, 1009-1010, 1105-1107).

## Verified (excerpts match the code at dffdf879)

- `PatchShield.cs`: Install :113-236, the comment at :119-123 and the log string at :124-126, the
  finalizers at :238-271 (quoted exactly), ShouldSwallow :273-313 with the `?.` reads at :299-300,
  TryUnpatchOffendingPatches :315-411 (early return :317, key :326, the unpatch log :393), the
  own-assembly skip :165-176, the SaveShield skip :197-202, `nameof` resolution :133-138, and the
  past-tense comment :178-179.
- `PatchShieldPolicy.cs`: :67-76, :69, :94-96, :123, :229-232 (the en dash at :232 is real),
  exclusion lists :77-148, protected prefixes (the "taom" test owner is protected, "thirdparty..." is
  not).
- `RethrowStackPreserver.cs`: :63, Marker :120-122, Describe :128-133.
- `RethrowStackPreserverTests.cs`: :108-115 and :336-355 (the IL scan) as described.
- `Dependencies/SubModule.cs:234` and `:293`; `TAOM.Dependencies.csproj:32` (InternalsVisibleTo) and
  `:70` (Lib.Harmony 2.4.2); `TAOM.Tests.csproj:19`; `CoopPresencePolicy.cs:35-48`.
- `RuntimeLog.Path` walks three directories up from the DLL, which gives `TAOM.Tests/bin/diag.log`.
  `git check-ignore` confirms the top-level `bin/` rule ignores it.
- 0Harmony 2.4.2 decompile (`E:\Decompiled_Bannerlord\_modules_build\TAOM.Dependencies__0Harmony.cs`,
  AssemblyFileVersion 2.4.2.0): :3609, :3806-3810, :4397-4416, :4862, :6582 and :1614-1679 all match.
  `GetRealMethod` returns the frame's own `method` object when the map misses (:1657), so the
  `ReferenceEquals` test in B4 is sound for non-replacement frames. The replacement's try block opens
  before the prefixes (`CreateReplacement`, :3044, before `AddPrefixes()`), so a throwing foreign prefix reaches the
  finalizer as B1 assumes.
- All seven "living copies" and all nine present-tense rows match the live lines, including the
  dashes the plan spells out. The RCA lines :33, :47 and :49, `followup-patchshield.md:239-240`, and
  the 1.4.5 finalizers at :244/:255 also match.
- The full-repo grep for `50µs`, `50 µs` and `microsecond` finds no other live copy in the A1 grep
  scope. Hits elsewhere are dated records or plans.
- Baseline totals match `plans/_audit/2026-10-02-perf/baseline.md`. The version is `v2.0.32`.
  Registry `Release` is 533509 (4.8.1). No `Directory.Build.*`, `global.json` or `nuget.config` sits
  in `E:\repos\taom-perf\scratch` or any ancestor. Lib.Harmony 2.4.2 (net472) is in the global
  packages folder, and the v4.7.2 targeting pack is installed.
- I tested the dash-count pipeline (lines 644, 1046) in Git Bash. It works with `LC_ALL=C.UTF-8` and
  fails without it, as the plan says.
- The plan's prose has no em or en dash. It contains no secrets and no worktree path. It names only
  the trunk `bannerlord-1.4.5`, in an orchestrator step. It has no CHANGELOG step and no protected
  file, and every dotnet command it gives is non-deploying with `-p:ModuleId=`.

## Blocking

1. **B4's unbounded stack walk can name the wrong method, and the swallow path then unpatches
   that wrong method** (lines 979-999, and the reasoning at 1002-1003).
   `ResolveShieldedOriginal` returns the first frame anywhere up the stack that Harmony recognises
   as a replacement. The finalizer is always called by its own replacement, which is the frame
   directly above it. If that frame does not resolve, the loop keeps going and returns the original
   of an OUTER shielded method that is also on the stack. That happens when `GetMethod()` yields a
   non-`MethodInfo`, or when another Harmony copy without a shared `originals` field built the
   replacement: `HarmonySharedState` falls back to a private map when the shared type lacks that
   field (:1544-1559). With a wrong original:
   - `ShouldSwallow` logs the wrong method name.
   - `TryUnpatchOffendingPatches` strips every non-protected owner's prefixes, postfixes and
     transpilers from an innocent method. This is an irreversible process-wide mutation that the
     current code (exact `__originalMethod`) cannot cause.

   None of the 8 tests can catch it, because every test frame resolves. The plan's own fallback
   contract (lines 975-977: "Null when no frame resolves... nothing is unpatched") is the safe
   behaviour, but the loop does not deliver it in this case.

   **Fix:** bound the walk. Stop at the first frame whose method has no `DeclaringType` (a dynamic
   method, which is what a Harmony replacement is). Return its original if it resolves, otherwise
   null. The B3 probe still passes, because Probe.Finalizer has a declaring type and is skipped.
   Add a RED test for it: a shielded or probe-patched `Targets.Outer` that invokes a plain
   `DynamicMethod` (unknown to Harmony), whose body calls `PatchShield.ResolveShieldedOriginal()` and
   stores the result. Expect null. The unbounded loop returns `Outer`. Add the case to the
   Maintenance notes and to the STOP list ("the lookup returns a method other than the one whose
   replacement called the finalizer").

## Non-blocking

1. Line 637 uses `<threads>`, but Step 3's placeholder list (lines 609-612) does not define it. Add
   `<threads>` = the thread count from the "contended:" line.
2. Branch B's stale-claim sweep is narrower than the claims it retires (lines 1041-1043):
   - The grep covers only `Dependencies TAOM.Tests docs/features docs/reference`. It skips
     `docs/reviews/lessons` (harmony-il.md:192-194 says "That finalizer binds" with
     `__originalMethod` on the next line, so a one-line grep misses it), `docs/migration`
     (dr3-maintenance.md:260, "the shield finalizer binds `__originalMethod`") and `docs/research`.
   - `docs/reference/harmony-patch-registry.md:1069` ends "so none carries an `__originalMethod`
     finalizer and its `GetMethodFromHandle` on every call". That is a present-tense mechanism claim
     about PatchShield's finalizer, and Branch B makes it stale. Lines 237-242 say only that the
     registry has no cost figure, which is true. The line belongs in the present-tense table, in
     Branch B scope and in the drift check.
3. `RethrowStackPreserver.cs:60` (out of scope, must not change) documents usage as
   `PreserveForRethrow(__exception, __originalMethod)`, which is stale after Branch B. Name it in
   the Maintenance notes as a known stale example, or justify leaving it.
4. Line 629-630, the draft for harmony-il.md:6: "#331's per-call figure was that stall divided..."
   has no antecedent for "that stall" in line 6's sentence. Name it ("#331's 104 to 109 s
   tournament-exit stall").
5. Some checks are judgments, not commands:
   - The second bullet of B5's Verify ("read each hit", line 1042).
   - The done criterion "shows no parameter declaration (only comments)" (line 1089). A mechanical
     form: `git grep -n -E "MethodBase\??\s+__originalMethod" -- Dependencies/Foundation/PatchShield.cs`
     returns nothing.
6. Typos: line 647 has a stray "(no new em or en dash)." after the sentence, and line 1049 ends with
   "..".
7. The drift check (line 9) omits files whose excerpts the plan relies on: `CoopPresencePolicy.cs`,
   `RuntimeLog.cs`, `DiagLog.cs`, `Dependencies/SubModule.cs`, both csproj files, and (per item 2)
   `harmony-patch-registry.md`.
8. Line 140 cites "the decompile dump's `_modules_build/...`" without saying where the dump lives
   (`E:\Decompiled_Bannerlord\`). The executor does not need it, but a re-verifier does.
9. Blast radius for `PatchShieldPolicy` (lines 273-274) is paraphrased, not quoted output. It is
   comments-only in this plan, so this is low risk.

## Excerpt mismatches

None found.

## Summary

The plan is concrete and self-contained, and a weak executor can follow it. Its measurement,
branching thresholds, TDD order and commands are well specified. One design defect needs fixing
before dispatch: B4's lookup must be bounded to the replacement that called the finalizer, with a
RED test proving that an unresolved immediate frame gives null, never an outer method.
