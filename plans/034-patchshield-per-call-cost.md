# Plan 034: Measure PatchShield's per-call cost and make the no-exception path free if it matters

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat dffdf879..HEAD -- Dependencies/Foundation/PatchShield.cs Dependencies/Foundation/PatchShieldPolicy.cs Dependencies/Foundation/RethrowStackPreserver.cs Dependencies/Foundation/SaveShield.cs Dependencies/Foundation/CoopPresencePolicy.cs Dependencies/Foundation/RuntimeLog.cs Dependencies/Foundation/DiagLog.cs Dependencies/SubModule.cs Dependencies/TAOM.Dependencies.csproj TAOM.Tests/TAOM.Tests.csproj TAOM.Tests/Infrastructure/Dependencies TAOM.Tests/Features/TrollBruteForce/Patch92BindingTests.cs TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/troll-brute-force.md docs/features/arena.md docs/features/crash-report.md docs/migration/dr3-maintenance.md docs/reviews/lessons/harmony-il.md docs/research/bannerlordcoop-internals.md docs/reference/harmony-patch-registry.md docs/reference/engine/submodule-lifecycle-and-harmony.md`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M (S if the measurement says "not material")
- **Risk**: MED (Branch B edits the crash-safety finalizers every shielded method runs; Branch A is
  comments and docs only, LOW)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (untranslated keys; the paid
  translator run waits on the maintainer). Python suite: not recorded, and this plan touches no
  `tools/` file, so it does not run it.
- **Issue**: filed by the orchestrator before execution

## Why this matters

PatchShield (in the separate `TAOM.Dependencies` assembly) attaches a Harmony finalizer to every
patched method in the process that it does not exclude: 159 methods at the first game start of a
process and 303 to 305 at the second (diag.log `shield pass` lines, 2026-10-02). Both of its
finalizers take `MethodBase __originalMethod`, which makes Harmony's generated wrapper call
`MethodBase.GetMethodFromHandle` on **every call** of the patched method, although PatchShield only
uses the value after an exception. Several shielded methods run per frame, per hit, or for every AI
agent on worker threads. Nobody has measured what that costs per call: the "~50 µs" figure in the
code comments and docs is #331's tournament-exit stall divided by an estimated call count, not a
measurement. This plan measures it on the game's runtime and, only if the binding costs 50 ns or more
per call, removes it while keeping every swallow, log line, unpatch and rethrow exactly as it is.
Either way the wrong figure is corrected.

## Current state

### Files

- `Dependencies/Foundation/PatchShield.cs`: the shield. `Install` (:113-236) attaches the finalizers;
  `ShieldFinalizerVoid` (:246) and `ShieldFinalizerWithResult` (:264) run on every call;
  `ShouldSwallow` (:273-313) and `TryUnpatchOffendingPatches` (:315-411) run only after an exception.
- `Dependencies/Foundation/PatchShieldPolicy.cs`: the pure policy (exclusion lists, co-op install
  gate). Its comments at :67-76, :123 and :229-238 carry the cost claim.
- `Dependencies/Foundation/RethrowStackPreserver.cs`: keeps a rethrown exception's stack. Its
  contract and its tests (`TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs`) must
  stay green **unchanged**.
- `Dependencies/SubModule.cs:234` (pass 1, `OnSubModuleLoad`) and `:293` (pass 2,
  `OnGameInitializationFinished`) call `PatchShield.Install()`. Not edited.
- `TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs`: the pattern for tests that
  patch real methods in-process with Lib.Harmony 2.4.2. It attaches PatchShield's real finalizers by
  name (:108-115) and IL-scans both finalizers for a direct `RethrowStackPreserver.PreserveForRethrow`
  call (:336-355, `ShieldFinalizers_EveryRethrowingFinalizer_CallsPreserveForRethrow`).

### The finalizers today (`Dependencies/Foundation/PatchShield.cs:238-271`)

```csharp
    /// <summary>
    /// Finalizer for void-return methods. Catches the swallow-trinity and returns
    /// silently to suppress the exception; non-matching exceptions are re-thrown by
    /// returning the ORIGINAL exception (Harmony Finalizer convention).
    ///
    /// Harmony calls this on EVERY call of the patched method, with a null exception when
    /// nothing threw, so the no-exception path must stay one null check.
    /// </summary>
    private static Exception? ShieldFinalizerVoid(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null || ShouldSwallow(__originalMethod, __exception)) return null;

        // Harmony rethrows a returned exception with `throw` (this finalizer returns a value, so
        // the wrapper never uses `rethrow`), which would replace its stack trace with the frames
        // from this method outward (player bundle 2d446100: a childbirth failure reported as five
        // frames ending at MapState.OnTick_Patch2).
        Interlocked.Increment(ref _rethrown);
        return RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);
    }

    /// <summary>
    /// Finalizer for return-value methods. Same swallow behavior; the patched method
    /// returns its zero/default value when we swallow because we don't have access
    /// to <c>__result</c> in a Finalizer (Harmony quirk). Acceptable trade-off:
    /// the caller gets a "stub" return value, which is far better than a crash.
    /// </summary>
    private static Exception? ShieldFinalizerWithResult(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null || ShouldSwallow(__originalMethod, __exception)) return null;

        // Same rethrow as ShieldFinalizerVoid; see there.
        Interlocked.Increment(ref _rethrown);
        return RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);
    }
```

The original method is used only on the exception path, in three places:

- `ShouldSwallow` (:297-305), for a `MissingMethodException`, `MissingFieldException` or
  `TypeLoadException` (after unwrapping `TargetInvocationException`):
  ```csharp
                var owner = originalMethod?.DeclaringType?.FullName ?? "?";
                var name = originalMethod?.Name ?? "?";
                DiagLog.Log(Tag, $"swallowed {ex.GetType().Name} from a patch on {owner}.{name}: {ex.Message}");
            ...
            TryUnpatchOffendingPatches(originalMethod, ex);
            return true;
  ```
- `TryUnpatchOffendingPatches` (:315-411): returns at once when `originalMethod == null`; otherwise
  keys a dedupe set by `$"{originalMethod.Module.ModuleVersionId}:{originalMethod.MetadataToken}"`
  (:326), adds the key to `_unpatched` (backs `PatchShield.UnpatchedCount`) when co-op is not active,
  reads `Harmony.GetPatchInfo(originalMethod)`, refuses protected owners
  (`PatchShieldPolicy.IsProtectedOwner`, a case-insensitive prefix match, so any owner id starting
  "taom" is protected), and for every other owner calls
  `harmony.Unpatch(originalMethod, HarmonyPatchType.Prefix|Postfix|Transpiler, owner)` and logs
  `$"unpatched owner '{owner}' on {targetKey}"` (:393).
- `RethrowStackPreserver.PreserveForRethrow(exception, rethrowSite)` (`RethrowStackPreserver.cs:63`):
  the marker line it writes is
  `"   --- End of stack trace from previous location (rethrown by a Harmony finalizer on " + Describe(rethrowSite) + ") ---"`
  (:120-122), where `Describe` is `DeclaringType.FullName + "." + Name`, or just `Name` when there is
  no declaring type, and "an unknown method" when `rethrowSite` is null. A Harmony replacement method
  has no declaring type, so passing the replacement instead of the original would print
  `<original's full type name>.<Method>_Patch<N>`.

`TAOM.Dependencies.csproj:32` declares `<InternalsVisibleTo Include="TAOM.Tests" />`, so tests can
call an `internal static` member of `PatchShield`. `DiagLog.Log` writes to `RuntimeLog.Path`, which in
the test process resolves to `TAOM.Tests/bin/diag.log` (git-ignored by the top-level `bin/` rule);
`RuntimeLog.Path` is public. `CoopPresence.IsActive` is false in the test process (no known co-op
module id is active and there is no `coop-force-active.flag`; `CoopPresencePolicy.ResolveActiveIds`,
`CoopPresencePolicy.cs:35-48`), so the unpatch path is armed in tests. If a Step B1 swallow test shows
a `(withheld)` line instead of `unpatched owner`, that assumption is false: STOP.

### What Harmony emits for `__originalMethod` (Lib.Harmony 2.4.2, the version TAOM ships)

`Dependencies/TAOM.Dependencies.csproj:70` and `TAOM.Tests/TAOM.Tests.csproj:19` both reference
`Lib.Harmony` 2.4.2. In the 0Harmony 2.4.2 decompile (on the desktop, the decompile dump's
`Decompiled_Bannerlord/_modules_build/TAOM.Dependencies__0Harmony.cs` at the root of the E: drive,
`AssemblyFileVersion("2.4.2.0")`; the executor does not need it, a re-verifier does),
`PatchFunctions.UpdateWrapper` builds every replacement with `MethodCreator` (:4862), and for an
injected `__originalMethod` parameter `MethodCreator` emits (:3806-3810):

```csharp
				case InjectionType.OriginalMethod:
					if (!EmitOriginalBaseMethod(original, list))
					{
						list.Add(Code.Ldnull);
					}
					continue;
```

with `MethodCreatorTools.EmitOriginalBaseMethod` (:4397-4416):

```csharp
			if (original is MethodInfo operand)
			{
				codes.Add(Code.Ldtoken[operand, null]);
			}
			...
			codes.Add(Code.Call[reflectedType.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1, null]);
```

where `m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[1] { typeof(RuntimeMethodHandle) })`
(:3609). The wrapper passes the finalizer's arguments on every call, so this runs on every call.

### The Harmony API Branch B relies on (same decompile)

```csharp
		public static MethodBase GetOriginalMethodFromStackframe(StackFrame frame)   // class Harmony, :6582
		{
			if (frame == null) throw new ArgumentNullException("frame");
			return HarmonySharedState.GetStackFrameMethod(frame, useReplacement: false);
		}
```

`HarmonySharedState.GetStackFrameMethod` (:1660-1679) takes `frame.GetMethod() as MethodInfo` and
calls `GetRealMethod` (:1636-1656), which looks the method's `Identifiable()` form up in the
`originals` map that `UpdatePatchInfo` (:1614-1634) fills with `replacement -> original` on every
patch, and **returns the frame's own method unchanged when it is not a known replacement**. When
`frame.GetMethod()` is not a `MethodInfo` it returns null on .NET Framework (its fallback,
`methodAddressRef`, is only set on Mono, :1525-1532). That a replacement's stack frame resolves
through this map on .NET Framework was checked once while revising this plan, in a scratch console
program outside the repository (net472, x64, Release, `.NET Framework 4.8.9345.0`, Harmony
`2.4.2.0`), using the exact lookup Step B4 prescribes. Its frame dump and results:

```
== bounded (judge one frame)
   frame: RuntimeMethodInfo Probe.Finalizer
   frame: RTDynamicMethod <no type>.Targets.Probed_Patch1
   frame: RuntimeMethodInfo Program.Main
probe finalizer resolved: Targets.Probed; handle equal: True
called from Main: null
   frame: RTDynamicMethod <no type>.Plan034UnknownFrame
   frame: RTDynamicMethod <no type>.Targets.OuterOfUnknownFrame_Patch1
   frame: RuntimeMethodInfo Program.Main
unknown dynamic frame under patched Outer: null
nested shield finalizers saw: Targets.Inner, Targets.Outer
```

The same program with the climbing walk (the RED variant in Step B4) printed
`unknown dynamic frame under patched Outer: Targets.OuterOfUnknownFrame`, with the other lines
unchanged. Inside the MSTest host this is still unproven: Branch B's tests prove it before anything
relies on it.

Three more facts from the same decompile shape the lookup in Step B4:

- **A frame can fail to resolve.** The map is the shared one only when the process-wide
  `HarmonySharedState` type carries an `originals` field; otherwise each Harmony copy keeps a
  private map (:1544-1559), so TAOM's copy cannot map a replacement another copy built. A frame whose
  `GetMethod()` is not a `MethodInfo` also resolves to nothing.
- **A replacement does not always lack a declaring type.** `DynamicMethodDefinition.Generate`
  (:133597-133650) builds a `DynamicMethod` (no declaring type) by default, but a method on a
  `MethodBuilder` type in a dynamic assembly (a declaring type) when `Debug` is set, when the
  `DMDType` switch says so, or when the replacement's IL has a filter or fault handler. (In the
  scratch program, patching a method with a `catch ... when` filter plus a finalizer threw
  `InvalidOperationException: Incorrect code generation for exception block` from
  `DMDEmitMethodBuilderGenerator` at patch time, so that last route may never carry a finalizer.)
- **An unknown dynamic method looks up safely.** On .NET Framework, `GetIdentifiable` maps the
  `RTDynamicMethod` a stack frame reports to its owning `DynamicMethod` (:76406-76413) before the map
  lookup, and on a miss `GetRealMethod` returns the frame's own method object (:1657), so a dynamic
  method Harmony never built compares `ReferenceEquals` to its frame's method.

So the lookup must judge **exactly one frame**: the one that called the finalizer, which is the
replacement running it. A walk that keeps climbing when that frame does not resolve would return an
OUTER shielded method that happens to be on the same stack; `ShouldSwallow` would then log the wrong
name and `TryUnpatchOffendingPatches` would strip every non-protected owner's prefixes, postfixes and
transpilers from an innocent method, a process-wide change nothing undoes. Stopping at "the first
frame with no declaring type" is not enough either, because of the `MethodBuilder` case above. The
caller's own frames are recognised by their declaring type (the finalizer's class), which also
covers a JIT that inlines the finalizer into the replacement.

### The cost claim and where it appears

The claim's source, `docs/reviews/rca-tournament-exit-hang-2026-07-06.md:47`, a dated record that
this plan does not edit: "Harmony's generated wrapper then executes `GetMethodFromHandle` + try/catch
per invocation (~50µs). ~10^6 × ~50µs ≈ 107s." The same RCA measured the fix as "exit
105-109s → **9.5s**" (:49) and records "The gen0 GC delta was **+8,276 in all three measured
hangs**" (:33). So the figure is a whole stall divided
by an estimated call count, on one workload; no per-call measurement exists
(`plans/_audit/2026-09-23-opus/followup-patchshield.md:239-240` lists it as UNMEASURED).

Living copies of the number (each must be corrected in both branches):

| File:line | Current text (excerpt) |
|---|---|
| `Dependencies/Foundation/PatchShieldPolicy.cs:69` | `// MethodBase.GetMethodFromHandle + try/catch on EVERY CALL (~50µs). The Gauntlet` |
| `Dependencies/Foundation/PatchShieldPolicy.cs:231` | `/// <c>MethodBase.GetMethodFromHandle</c> plus a try/catch on EVERY CALL (~50 µs). That tax is` |
| `docs/features/coop-interop.md:406` | ``MethodBase.GetMethodFromHandle` plus a try/catch **on every call** (~50 µs) [em dash] the same mechanism`` |
| `docs/reviews/lessons/harmony-il.md:6` | `... + try/catch on EVERY invocation (~50µs + allocation). Harmless on campaign-tick methods; ...` |
| `docs/reviews/lessons/harmony-il.md:194` | `try/catch **per call** (~50 µs). The population it wraps is therefore chosen by whatever else is` |
| `docs/features/arena.md:10` | `... stacked a `__originalMethod`-binding finalizer on every patched method, adding ~50µs of reflection per call. Fix: ...` |
| `docs/migration/dr3-maintenance.md:260` | `... the shield finalizer binds `__originalMethod`, so Harmony's wrapper pays `GetMethodFromHandle` + try/catch per CALL (~50µs); stacked on UIExtenderEx's ...` |

Dated historical records that also carry the number and are **not** edited (they record what was
believed at the time): `docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md:23580`,
`docs/reviews/rca-tournament-exit-hang-2026-07-06.md:47`, `docs/reviews/rca-herorace-patch72-2026-08-21.md:113`
("the ~50 microsecond finalizer tax"), and the `docs/reviews/codex-adversarial-*` prompt files.

Present-tense statements that PatchShield's finalizer binds `__originalMethod` (Branch B rewrites
these, because they become false; Branch A leaves them, because they stay true):

| File:line | Current text (excerpt) |
|---|---|
| `Dependencies/Foundation/PatchShield.cs:119-123` | `// Player-reported 2026-08-02: this is a frame-rate fix, not a safety change. Coop's` / `// AutoSync transpiles every declared method of 43 campaign types, and shielding that` / `// surface makes every one of them pay the __originalMethod binding tax per call [em dash]` / `// the same mechanism as the #331 tournament freeze. Rationale + what it gives up:` / `// PatchShieldPolicy.ShouldInstall.` |
| `Dependencies/Foundation/PatchShieldPolicy.cs:67-69` | `// Issue #331 round 2 (2026-07-09, measured): NEVER shield the Gauntlet/2D UI layer.` / `// A shield finalizer binds __originalMethod, so Harmony's generated wrapper pays a` / `// MethodBase.GetMethodFromHandle + try/catch on EVERY CALL (~50µs). The Gauntlet` |
| `Dependencies/Foundation/PatchShieldPolicy.cs:123` | `/// makes a per-call <c>__originalMethod</c> finalizer tax unacceptable: <c>Formation.get_UnitDiameter</c>` |
| `Dependencies/Foundation/PatchShieldPolicy.cs:229-232` | `/// NO under co-op, and this is a PERFORMANCE decision, not a correctness one. A shield finalizer` / `/// binds <c>__originalMethod</c>, so Harmony's generated wrapper pays a` / `/// <c>MethodBase.GetMethodFromHandle</c> plus a try/catch on EVERY CALL (~50 µs). That tax is` / `/// what turned a millisecond tournament teardown into a measured 104[en dash]109 s freeze in #331, and` |
| `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs:130-133` | `// Player-reported 2026-08-02: shielding BannerlordCoop's AutoSync surface collapsed frame rate.` / `// Every declared method of 43 campaign types gets a finalizer that binds __originalMethod, so` / `// Harmony's wrapper pays GetMethodFromHandle + try/catch per call [em dash] the #331 mechanism, on the` / `// campaign hot path. These four rows pin the gate so it cannot regress in either direction.` |
| `TAOM.Tests/Features/TrollBruteForce/Patch92BindingTests.cs:45-47` | `// ... missing from that list would pay the per-call` / `// __originalMethod finalizer on a per-unit hot path (the #331 cost), with every other test still green.` |
| `TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs:475-476` | `// Each is asked for every agent, often on the TWParallel workers: a per-call PatchShield finalizer would take` / `// the reflection cache's lock on every call (rca-tournament-exit-hang-2026-07-06.md).` |
| `docs/features/coop-interop.md:405-407` | `A shield finalizer binds `__originalMethod`, so Harmony's generated wrapper pays a` / ``MethodBase.GetMethodFromHandle` plus a try/catch **on every call** (~50 µs) [em dash] the same mechanism`` / `that turned a millisecond tournament teardown into a measured 104[en dash]109 s freeze in #331. Co-op` |
| `docs/features/bannerlord-together-compat.md:79-82` | `safety one. A shield finalizer binds `__originalMethod`, so Harmony's generated wrapper pays a` / ``MethodBase.GetMethodFromHandle` plus a try/catch on every call [em dash] the same mechanism that turned a`` / `millisecond tournament teardown into a measured 104[en dash]109 s freeze in #331. A co-op mod that` / `transpiles the whole campaign surface converts that per-call tax into a frame-rate collapse; a` |
| `docs/features/troll-brute-force.md:74` | `because a shield finalizer would add a `GetMethodFromHandle` to every per-unit call. MixedFormations spaces its` |
| `docs/reviews/lessons/harmony-il.md:192-193` | `PatchShield attaches a finalizer to every method Harmony has patched. That finalizer binds` / ``__originalMethod`, so Harmony's generated wrapper pays a `MethodBase.GetMethodFromHandle` plus a`` |
| `docs/migration/dr3-maintenance.md:260` (one long line) | `... [em dash] the shield finalizer binds `__originalMethod`, so Harmony's wrapper pays `GetMethodFromHandle` + try/catch per CALL (~50µs); ...` |
| `docs/reference/harmony-patch-registry.md:1069` (one long line, ends with) | `... PatchShield's shield pass skips all five targets (`PatchShieldPolicy.ExcludedTargetMethods` lists ...), so none carries an `__originalMethod` finalizer and its `GetMethodFromHandle` on every call.` |

(Where an excerpt above shows `[em dash]` or `[en dash]`, the live line holds that character; the
plan spells it out so it carries none itself. The words you write must not add one: use a comma, a
colon or parentheses, and write ranges as "104 to 109 s". Dashes elsewhere on a long line you touch,
in words you are not rewriting, may stay.) Past-tense or incident lines that stay
accurate in both branches and are not edited: `PatchShield.cs:178-179`, `PatchShieldPolicy.cs:94-96`,
`harmony-il.md:672` and `:692` (dated incident lessons), `docs/features/bannerlord-together-compat.md:447`,
`docs/features/crash-report.md:283`, `docs/research/bannerlordcoop-internals.md:337`. The log string
at `PatchShield.cs:125` (`"PatchShield install skipped (finalizer tax on Coop's AutoSync surface)"`)
is runtime output and stays in both branches.

**The stale-claim grep.** This command finds every row of the present-tense table, including the
claims wrapped across two lines, over the whole repository except the dated records and the plans
(Step B5 uses it):

```bash
git grep -n -I -E -e 'binds (`|<c>)?__originalMethod' -e 'finalizer binds[[:space:]]*$' -e 'binding tax' -e 'finalizer tax' -e 'carries an `__originalMethod` finalizer' -e 'would add a `GetMethodFromHandle`' -e 'would pay the per-call[[:space:]]*$' -e 'finalizer would take[[:space:]]*$' -- . ':!plans' ':!docs/changelog-archive' ':!docs/reviews/rca-*' ':!docs/reviews/codex-adversarial-*'
```

At `dffdf879` it prints 19 lines: the 13 table rows (by their first matching line) plus these six,
which are accurate in both branches and stay: `PatchShield.cs:125` (the log string),
`bannerlord-together-compat.md:447` (a dated changelog entry), `crash-report.md:283` (what pass 2
did before plan 007), `bannerlordcoop-internals.md:337` (past tense), `harmony-il.md:6` (a general
statement about any Harmony patch that binds `__originalMethod`, still true) and `harmony-il.md:672`
(a dated incident).

`docs/reference/harmony-patch-registry.md` carries no per-call cost figure for PatchShield
(`git grep -n -e "50µs" -e "50 µs" -- docs/reference/harmony-patch-registry.md` returns nothing at
`dffdf879`; its one `GetMethodFromHandle` mention is the :1069 row above), so this plan records the
measured figure in the engine reference instead: `docs/reference/engine/submodule-lifecycle-and-harmony.md:35`, the **Finalizer** bullet, which
today reads "**Finalizer** [em dash] catches exceptions thrown by the original/other patches (PatchShield
wraps every patch in one). Returning `null` swallows. ..." and says nothing about cost.

### Which hot methods carry the shield

`Install` skips a method only when it is declared in an assembly whose name starts "TAOM" (:165-176),
when `PatchShieldPolicy` excludes it (namespaces `TaleWorlds.GauntletUI`, `TaleWorlds.TwoDimension`,
`TaleWorlds.MountAndBlade.GauntletUI`, `ManagedCallbacks`; methods `Formation.get_UnitDiameter`,
`GetUnitPositionWithIndexAccordingToNewOrder`, `GetUnitSpawnFrameWithIndex`, `Agent.GetPrimaryWieldedItemIndex`,
`GetOffhandWieldedItemIndex`, `GetMissileRange`, `Mission.CanAgentRout`; `PatchShieldPolicy.cs:77-148`),
or when SaveShield owns it (:197-202). TAOM patches engine methods such as `Formation.SetMovementOrder`,
`Agent.CheckToDropFlaggedItem`, `Agent.HandleBlowAux`, `Mission.TickAgentsAndTeamsImp` and
`MissionState.TickMissionAux`, which none of those rules skip, so pass 2 shields them.
`.claude/rules/csharp-architecture.md` ("Mission-scope agent handles and the engine's threads") records
that `Formation.SetMovementOrder` from the AI and `Formation.GetOrderPositionOfUnit` for AI units run off
the main thread, on the AI thread and the TWParallel worker pool. That is why Step 2 measures
contended calls as well as single-threaded ones.

### Blast radius

`python tools/graphify_taom.py affected "PatchShield" --depth 2` (graph built from `dffdf879`):

```
Affected nodes for PatchShield
- .OnGameInitializationFinished() [calls] Dependencies/SubModule.cs:L293
- .OnSubModuleLoad() [calls] Dependencies/SubModule.cs:L234
```

The graph does not see reflection: `RethrowStackPreserverTests.cs:108-115` and `:344-345` reach both
finalizers by name through `AccessTools.Method(typeof(PatchShield), "ShieldFinalizerVoid")`, and
`PatchShield.Install` resolves them by `nameof` (:133-138). Branch B keeps both names. No other code
references them (`git grep -n "ShieldFinalizer"` at `dffdf879`: only these, plus comments and docs).
`python tools/graphify_taom.py affected "PatchShieldPolicy" --depth 2` lists 34 nodes: 28 tests
(`PatchShieldPolicyTests`, plus `CreatureBanditsWiringTests.HotCreatureTargets_AreOnPatchShieldsExclusionList`
and `Patch92BindingTests.EveryPatch92Target_IsOnPatchShieldsHotMethodList`) and six code nodes, quoted:

```
- .IsExcludedTarget() [calls] Dependencies/Foundation/PatchShield.cs:L59
- .Install() [calls] Dependencies/Foundation/PatchShield.cs:L227
- .TryUnpatchOffendingPatches() [calls] Dependencies/Foundation/PatchShield.cs:L366
- .OnGameInitializationFinished() [calls] Dependencies/SubModule.cs:L293
- .OnSubModuleLoad() [calls] Dependencies/SubModule.cs:L234
- .ShouldSwallow() [calls] Dependencies/Foundation/PatchShield.cs:L305
```

This plan changes only `PatchShieldPolicy`'s comments.

### Conventions that bind this change

- **ADR-002** (thin entry points under 150 lines): not engaged; `PatchShield` is foundation
  infrastructure, not a feature entry point, and Branch B adds about 40 lines to it.
- **ADR-007** (services take adapters, never sealed TaleWorlds types): not engaged; no TaleWorlds
  type is touched.
- **ADR-008** (testability): every behaviour Branch B touches gets a real-Harmony test first
  (`.claude/rules/tests.md`: TDD, `MethodName_StateUnderTest_ExpectedBehavior` names, MSTest,
  NSubstitute only, never Moq).
- **ADR-003/004/005**: no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- `.claude/rules/csharp-architecture.md` threading rule: the shielded methods run on worker threads,
  so the exception-path lookup must be thread-safe (Harmony's `originals` map is read under its own
  lock, `GetRealMethod` :1639) and must never throw (wrap it in try/catch, as every other PatchShield
  helper is).
- `.claude/rules/provenance.md` covers `Dependencies/**/*.cs`: no outside code is copied here; the
  lookup uses Harmony's public API only, so no register row is needed.
- No em or en dash in any line you write (AGENTS.md "Human prose"); code spans are exempt.

## Commands you will need

Prefix every dotnet command with the TEMP and TMP your dispatch rules give, quoted.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (builds `TAOM.Dependencies` too) |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchShieldFinalizerTests"` | the named tests run; a filter matching nothing proves nothing |
| Rethrow contract | the same with `--filter "FullyQualifiedName~RethrowStackPreserverTests"` | all pass, none skipped |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Data | `python tools/validate_moduledata.py` | not needed: this plan touches no ModuleData |

Never `./build.ps1`: it deploys into the game install. No binding gate: this plan adds and changes no
TAOM patch category, and its new tests touch no engine type (so the RefAsm unit step is not needed).

## Scope

**In scope** (the only files you modify):

Both branches:
- `Dependencies/Foundation/PatchShieldPolicy.cs` (comments only)
- `docs/features/coop-interop.md`
- `docs/features/arena.md`
- `docs/migration/dr3-maintenance.md`
- `docs/reviews/lessons/harmony-il.md`
- `docs/reference/engine/submodule-lifecycle-and-harmony.md`

Branch B only, in addition:
- `docs/reference/harmony-patch-registry.md` (the :1069 clause only)
- `Dependencies/Foundation/PatchShield.cs`
- `TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs` (new)
- `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs` (comment only, :130-133)
- `TAOM.Tests/Features/TrollBruteForce/Patch92BindingTests.cs` (comment only, :45-47)
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs` (comment only, :475-476)
- `docs/features/bannerlord-together-compat.md`
- `docs/features/troll-brute-force.md`

Outside the repository: the benchmark project in your scratch folder (never committed).

**Out of scope** (do NOT touch, even though they look related):
- The exclusion lists (`ExcludedTargetNamespacePrefixes`, `ExcludedTargetMethods`), which methods
  PatchShield wraps, `ShouldInstall` and the co-op skip, and the pass timing. Whether a cheaper
  finalizer justifies shielding those methods again is a separate maintainer decision.
- `ShouldSwallow`'s exception classification, `TryUnpatchOffendingPatches`'s logic, the counters, and
  `RethrowStackPreserver.cs`. Branch B changes only where the original method comes from.
- `Dependencies/Foundation/SaveShield.cs` (its finalizer also binds `__originalMethod`, but its targets
  are save and load methods, not hot paths) and the `Main/Features/SaveLoadDiagnostics/Hooks/` finalizers.
- `RethrowStackPreserverTests.cs`: it must pass unchanged. Do not edit it.
- The dated historical records listed in Current state.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Dependencies/SubModule.cs`: not needed.
  No protected file is needed, so there is no Step 0.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry. STOP and report instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at `dffdf879`; a hook
  refuses any other).
  - Branch A: `docs(patchshield): <version> - record the measured per-call cost`
  - Branch B: `perf(patchshield): <version> - find the original method only on a throw`
- The body is the changelog entry, wrapped at 72: what changed and why, for a reader of the release
  note, and the measurement table from Step 2 (runtime, Harmony version, CPU, the four variants, the
  two deltas). Never edit `CHANGELOG.md`. No AI attribution trailer. Branch B adds
  `Not-tested: the frame-time effect in a live battle; the lookup on a replacement built by another
  mod's Harmony copy.` Write the message to a file in your scratch folder and run `git commit -F <file>`.
- Stage explicit paths only (never `-A`, `-u` or `.`). Never `--no-verify`.

## Steps

### Step 1: record the base

Run the drift check above, then the full test suite before any edit, and write its totals and failing
tests into your report.

**Verify**: the totals line is `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` and the
one failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`, or the difference is explained in your
report (for example the branch tip carries another plan's tests).

### Step 2: measure the per-call cost on the game's runtime

Build a console benchmark **outside the repository**, in a `bench` folder under your scratch folder.
First confirm no `Directory.Build.props` or `Directory.Build.targets` sits in that folder or any
parent (`ls` each ancestor); one would change the build. If one does, STOP.

Write `ShieldBench.csproj` with the Write tool:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>ShieldBench</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Lib.Harmony" Version="2.4.2" />
  </ItemGroup>
</Project>
```

Write `Program.cs` with the Write tool. Variant (b) is the finalizer shape PatchShield ships; (c) is
the same body without `__originalMethod`; (a) isolates the cost of being Harmony-patched at all; every
patched variant carries the same empty postfix, so the only difference between (a), (b) and (c) is
the finalizer.

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;
using Microsoft.Win32;

public static class Targets
{
    [MethodImpl(MethodImplOptions.NoInlining)] public static int Unpatched(int x) => x + 1;
    [MethodImpl(MethodImplOptions.NoInlining)] public static int PostfixOnly(int x) => x + 1;
    [MethodImpl(MethodImplOptions.NoInlining)] public static int WithOriginal(int x) => x + 1;
    [MethodImpl(MethodImplOptions.NoInlining)] public static int ExceptionOnly(int x) => x + 1;
}

public static class Patches
{
    public static void Postfix() { }

    // (b) PatchShield's shape at dffdf879: binds __originalMethod.
    public static Exception FinalizerWithOriginal(MethodBase __originalMethod, Exception __exception)
    {
        if (__exception == null || Sink(__originalMethod, __exception)) return null;
        return __exception;
    }

    // (c) The same body without __originalMethod.
    public static Exception FinalizerExceptionOnly(Exception __exception)
    {
        if (__exception == null || Sink(null, __exception)) return null;
        return __exception;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Sink(MethodBase original, Exception exception) => false;
}

public static class Program
{
    private const int CallsPerRun = 20_000_000;
    private const int Runs = 7;
    private const int CallsPerThread = 2_000_000;
    private const int ContendedRuns = 5;
    private static long _sink;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long LoopUnpatched(int n) { long s = 0; for (int i = 0; i < n; i++) s += Targets.Unpatched(i); return s; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long LoopPostfixOnly(int n) { long s = 0; for (int i = 0; i < n; i++) s += Targets.PostfixOnly(i); return s; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long LoopWithOriginal(int n) { long s = 0; for (int i = 0; i < n; i++) s += Targets.WithOriginal(i); return s; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long LoopExceptionOnly(int n) { long s = 0; for (int i = 0; i < n; i++) s += Targets.ExceptionOnly(i); return s; }

    public static int Main()
    {
        AppDomain.MonitoringIsEnabled = true;
        var cpu = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "?");
        var dbg = typeof(Program).Assembly.GetCustomAttribute<DebuggableAttribute>();
        Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; 64-bit: {Environment.Is64BitProcess}; debugger: {Debugger.IsAttached}; JIT optimizer disabled: {dbg?.IsJITOptimizerDisabled ?? false}");
        Console.WriteLine($"Harmony: {typeof(Harmony).Assembly.GetName().Version}; CPU: {cpu}; logical processors: {Environment.ProcessorCount}");

        var harmony = new Harmony("bench.patchshield.034");
        var postfix = new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(Patches.Postfix)));
        harmony.Patch(AccessTools.Method(typeof(Targets), nameof(Targets.PostfixOnly)), postfix: postfix);
        harmony.Patch(AccessTools.Method(typeof(Targets), nameof(Targets.WithOriginal)), postfix: postfix,
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(Patches.FinalizerWithOriginal))));
        harmony.Patch(AccessTools.Method(typeof(Targets), nameof(Targets.ExceptionOnly)), postfix: postfix,
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(Patches.FinalizerExceptionOnly))));
        foreach (var name in new[] { nameof(Targets.PostfixOnly), nameof(Targets.WithOriginal), nameof(Targets.ExceptionOnly) })
        {
            var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(Targets), name));
            Console.WriteLine($"patched {name}: postfixes {info?.Postfixes.Count ?? 0}, finalizers {info?.Finalizers.Count ?? 0}");
        }

        var variants = new (string Name, Func<int, long> Loop)[]
        {
            ("0 unpatched", new Func<int, long>(LoopUnpatched)),
            ("a postfix, no finalizer", new Func<int, long>(LoopPostfixOnly)),
            ("b postfix + finalizer(__originalMethod, __exception)", new Func<int, long>(LoopWithOriginal)),
            ("c postfix + finalizer(__exception)", new Func<int, long>(LoopExceptionOnly)),
        };

        // Warm-up and correctness: every variant computes the same sum.
        var expected = variants[0].Loop(1_000_000);
        foreach (var v in variants)
        {
            if (v.Loop(1_000_000) != expected) { Console.WriteLine($"MISMATCH in {v.Name}"); return 2; }
        }

        var single = variants.ToDictionary(v => v.Name, v => new List<double>());
        var bytes = variants.ToDictionary(v => v.Name, v => new List<double>());
        var gcs = variants.ToDictionary(v => v.Name, v => 0);
        for (int run = 0; run < Runs; run++)
        {
            foreach (var v in variants)   // interleaved, so drift hits every variant alike
            {
                single[v.Name].Add(MeasureSingle(v.Loop, out var bytesPerCall, out var gen0));
                bytes[v.Name].Add(bytesPerCall);
                gcs[v.Name] += gen0;
            }
        }

        int threads = Math.Min(Environment.ProcessorCount, 8);
        var contended = variants.ToDictionary(v => v.Name, v => new List<double>());
        for (int run = 0; run < ContendedRuns; run++)
        {
            foreach (var v in variants) contended[v.Name].Add(MeasureContended(v.Loop, threads));
        }

        Console.WriteLine();
        Console.WriteLine($"single thread: {Runs} runs x {CallsPerRun:N0} calls; contended: {threads} threads x {CallsPerThread:N0} calls, {ContendedRuns} runs");
        Console.WriteLine("variant | single ns/call median (min..max) | bytes/call | gen0 GCs | contended ns/call median (min..max)");
        foreach (var v in variants)
        {
            Console.WriteLine($"{v.Name} | {Median(single[v.Name]):F2} ({single[v.Name].Min():F2}..{single[v.Name].Max():F2}) | {Median(bytes[v.Name]):F1} | {gcs[v.Name]} | {Median(contended[v.Name]):F2} ({contended[v.Name].Min():F2}..{contended[v.Name].Max():F2})");
        }

        double singleDelta = Median(single[variants[2].Name]) - Median(single[variants[3].Name]);
        double contendedDelta = Median(contended[variants[2].Name]) - Median(contended[variants[3].Name]);
        Console.WriteLine();
        Console.WriteLine($"b - c single thread: {singleDelta:F2} ns/call");
        Console.WriteLine($"b - c contended: {contendedDelta:F2} ns/call");
        Console.WriteLine($"DECISION METRIC (larger of the two): {Math.Max(singleDelta, contendedDelta):F2} ns/call");
        Console.WriteLine($"sink {_sink}");
        return 0;
    }

    private static double MeasureSingle(Func<int, long> loop, out double bytesPerCall, out int gen0)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
        int collections = GC.CollectionCount(0);
        var sw = Stopwatch.StartNew();
        _sink += loop(CallsPerRun);
        sw.Stop();
        gen0 = GC.CollectionCount(0) - collections;
        GC.Collect();
        bytesPerCall = (AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocated) / (double)CallsPerRun;
        return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / CallsPerRun;
    }

    private static double MeasureContended(Func<int, long> loop, int threads)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        using var start = new ManualResetEventSlim(false);
        var workers = new Thread[threads];
        var sums = new long[threads];
        int ready = 0;
        for (int t = 0; t < threads; t++)
        {
            int index = t;
            workers[t] = new Thread(() => { Interlocked.Increment(ref ready); start.Wait(); sums[index] = loop(CallsPerThread); });
            workers[t].Start();
        }
        while (Volatile.Read(ref ready) < threads) Thread.Yield();
        var sw = Stopwatch.StartNew();
        start.Set();
        foreach (var w in workers) w.Join();
        sw.Stop();
        _sink += sums.Sum();
        return sw.Elapsed.TotalMilliseconds * 1_000_000.0 / CallsPerThread;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
```

Build it in Release (`dotnet build <bench>/ShieldBench.csproj -c Release`, with the TEMP and TMP
prefix; the package restores from the local NuGet cache), find `ShieldBench.exe` under
`bin/Release/net472/`, and run it directly (not under a debugger, not through `dotnet run`), with
`timeout 900`, redirecting its output to `result-1.txt` beside it. Do not run anything else heavy at
the same time.

**Verify** (all must hold, from `result-1.txt`):
- the `Runtime:` line starts `Runtime: .NET Framework 4.8` (the game's runtime; this desktop has
  4.8.1 installed, registry `Release` 533509) and shows `64-bit: True`, `debugger: False`,
  `JIT optimizer disabled: False`;
- the `Harmony:` line says `2.4.2.0`;
- the three `patched` lines show `postfixes 1` each, `finalizers 0`, `1`, `1` respectively;
- no `MISMATCH` line, exit code 0;
- the table has four rows and the `DECISION METRIC` line is present.

If any fails, STOP. Copy the whole output (it holds no secrets) into your report.

### Step 3: decide

Read `DECISION METRIC` from `result-1.txt`. It is the larger of the single-threaded and the contended
`(b) - (c)` difference, because several shielded methods run on worker threads.

- Below 40 ns: take **Branch A** (Steps A1 to A2).
- 60 ns or more: take **Branch B** (Steps B1 to B6).
- From 40 up to 60 ns: run the benchmark once more into `result-2.txt`. If both runs fall on the same
  side of 50 ns, take that side's branch (below 50: A; 50 or more: B) and quote both runs. If they
  fall on different sides, STOP and report both outputs.

Record the decision and the numbers in your report. Use these values in every text below:
`<D1>` = `b - c single thread`, `<D2>` = `b - c contended`, `<A>` = the single-threaded median of row
(a), `<C>` = that of row (c), `<B>` = that of row (b), `<BYTES>` = row (b)'s bytes/call, `<CPU>` = the
CPU from the `Harmony:` line, `<threads>` = the thread count on the `contended:` line ("contended:
N threads x ..."). Round to whole nanoseconds in prose.

### Step A1 (Branch A): correct the figure, keep the design

Edit each "living copy" row of the cost-claim table in Current state. Replace only the parenthesised
figure and the words around it that state the figure; keep the rest of each sentence. Use this
draft wording and adapt it to each sentence (it is a draft: re-read each sentence after editing and
make sure it states only what Step 2 measured):

- In `PatchShieldPolicy.cs:69` and `:231`: replace `(~50µs)` / `(~50 µs)` with
  `(about <D1> ns per call more than a finalizer without it, <D2> ns with threads contending;
  measured on .NET Framework 4.8.1 with Harmony 2.4.2, plan 034)`. Re-wrap the comment lines so none
  passes column 120.
- In `coop-interop.md:406` and `harmony-il.md:194`: replace `(~50 µs)` with
  `(measured at about <D1> ns per call, <D2> ns contended, plan 034)`. On `coop-interop.md:406` also
  replace the em dash before "the same mechanism" with a comma.
- In `harmony-il.md:6`: replace `(~50µs + allocation)` with
  `(measured at about <D1> ns per call and <BYTES> bytes per call in isolation, plan 034; the earlier
  per-call figure was #331's 104 to 109 s tournament-exit stall divided by its estimated call count,
  not a measurement)`.
- In `arena.md:10`: replace `adding ~50µs of reflection per call` with
  `adding a reflection lookup to every call (about <D1> ns each, measured in isolation by plan 034)`.
- In `dr3-maintenance.md:260`: replace `(~50µs)` with `(about <D1> ns per call, plan 034)`.

Then add one sentence to the end of the **Finalizer** bullet in
`docs/reference/engine/submodule-lifecycle-and-harmony.md:35`:
`A finalizer that takes `__originalMethod` makes the wrapper call `MethodBase.GetMethodFromHandle` on every call: about <D1> ns per call more than one that takes only `__exception` (<D2> ns with <threads> threads contending, <BYTES> bytes per call), measured on .NET Framework 4.8.1 with Harmony 2.4.2 on <CPU> (plan 034, 2026-10).`

**Verify**:
- `git grep -n -F -e "50µs" -e "50 µs" -e "50μs" -e "50 μs" -e "50 microsecond" -- Dependencies TAOM.Tests docs/features docs/migration docs/reference docs/reviews/lessons`
  returns nothing.
- `python tools/lint_docs.py --fail-on-drift` exits 0.
- your edits add no em or en dash: the count from
  `git diff -U0 | LC_ALL=C.UTF-8 grep -P "^\+(?!\+\+)" | LC_ALL=C.UTF-8 grep -o -P "[\x{2013}\x{2014}]" | wc -l`
  is not larger than the count from the same command with `^-(?!--)` in place of `^\+(?!\+\+)`
  (without `LC_ALL=C.UTF-8`, Git Bash's grep rejects the pattern). Several edited lines are long
  paragraphs that already hold dashes elsewhere; those may stay, since you are not rewriting them.

### Step A2 (Branch A): build, test, commit

Run the build and the full test suite.

**Verify**: build exit 0; the totals line equals Step 1's; `git status --porcelain` lists only the six
"both branches" files. Commit them with the Branch A subject; the body states the measured numbers,
the method (Step 2's variants, run counts, runtime, CPU), and that the design is unchanged because the
binding costs under 50 ns per call. Then stop: Branch B does not run.

### Step B1 (Branch B): pin today's exception-path behaviour (characterization, green at base)

Create `TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs`, modelled on
`RethrowStackPreserverTests.cs`. These tests describe behaviour that must not change, so they pass on
the unmodified code and keep passing after Step B4.

```csharp
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// PatchShield's finalizers through real Harmony patches: what a shielded method does after a
/// missing-API exception (swallow, diag.log line, strip the foreign owner) and after any other
/// exception (rethrow with a marker naming the shielded method). Plan 034 moved where the finalizers
/// get the shielded method from; these pin that nothing they produce changed.
/// </summary>
[TestClass]
public class PatchShieldFinalizerTests
{
    // The shield's finalizers sit under a "taom" owner, which PatchShield never strips; the throwing
    // prefix belongs to an owner no protected prefix matches, so the rescue may strip it.
    private const string ShieldOwner = "taom.tests.patchshield-finalizer";
    private const string ForeignOwner = "thirdparty.tests.patchshield-foreign";
    private static Harmony _shield = null!;
    private static Harmony _foreign = null!;

    private static readonly string MarkerPrefix = "(rethrown by a Harmony finalizer on ";

    public static class Targets
    {
        [MethodImpl(MethodImplOptions.NoInlining)] public static void SwallowVoid() { }
        [MethodImpl(MethodImplOptions.NoInlining)] public static int SwallowWithResult() => 7;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void RethrowVoid() => throw new InvalidOperationException("034 rethrow");
        [MethodImpl(MethodImplOptions.NoInlining)] public static int RethrowWithResult() => throw new InvalidOperationException("034 rethrow");
        [MethodImpl(MethodImplOptions.NoInlining)] public static void InnerShielded() => throw new InvalidOperationException("034 nested");
        [MethodImpl(MethodImplOptions.NoInlining)] public static void OuterShielded() => InnerShielded();
    }

    public static class ForeignPatches
    {
        public static string NextMessage = "";
        public static void ThrowMissingMethod() => throw new MissingMethodException(NextMessage);
    }

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _shield = new Harmony(ShieldOwner);
        _foreign = new Harmony(ForeignOwner);
        var prefix = new HarmonyMethod(AccessTools.Method(typeof(ForeignPatches), nameof(ForeignPatches.ThrowMissingMethod)));
        _foreign.Patch(Target(nameof(Targets.SwallowVoid)), prefix: prefix);
        _foreign.Patch(Target(nameof(Targets.SwallowWithResult)), prefix: prefix);

        Shield(nameof(Targets.SwallowVoid), "ShieldFinalizerVoid");
        Shield(nameof(Targets.SwallowWithResult), "ShieldFinalizerWithResult");
        Shield(nameof(Targets.RethrowVoid), "ShieldFinalizerVoid");
        Shield(nameof(Targets.RethrowWithResult), "ShieldFinalizerWithResult");
        Shield(nameof(Targets.InnerShielded), "ShieldFinalizerVoid");
        Shield(nameof(Targets.OuterShielded), "ShieldFinalizerVoid");
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        _shield.UnpatchAll(ShieldOwner);
        _foreign.UnpatchAll(ForeignOwner);
    }

    private static MethodInfo Target(string name) => AccessTools.Method(typeof(Targets), name);

    private static void Shield(string target, string finalizer)
    {
        var method = AccessTools.Method(typeof(PatchShield), finalizer);
        Assert.IsNotNull(method, $"PatchShield.{finalizer} did not resolve");
        _shield.Patch(Target(target), finalizer: new HarmonyMethod(method));
    }

    private static long DiagLogLength()
    {
        var path = RuntimeLog.Path;
        Assert.IsFalse(string.IsNullOrEmpty(path), "RuntimeLog.Path did not resolve in the test process");
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    private static string DiagLogSince(long offset)
    {
        using var stream = new FileStream(RuntimeLog.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Exception Capture(Action call)
    {
        try { call(); }
        catch (Exception ex) { return ex; }
        throw new AssertFailedException("the target did not throw");
    }

    private static string Marker(string target) => MarkerPrefix + typeof(Targets).FullName + "." + target + ") ---";

    private static void AssertSwallowedLoggedAndStripped(string target, string message, long logOffset,
        long swallowedBefore, int unpatchedBefore)
    {
        var method = Target(target);
        var log = DiagLogSince(logOffset);
        Assert.AreEqual(1L, PatchShield.SwallowedMissingMethod - swallowedBefore, "one MissingMethodException swallowed");
        StringAssert.Contains(log, $"swallowed MissingMethodException from a patch on {typeof(Targets).FullName}.{target}: {message}");
        StringAssert.Contains(log, $"unpatched owner '{ForeignOwner}' on {method.Module.ModuleVersionId}:{method.MetadataToken}");
        Assert.AreEqual(1, PatchShield.UnpatchedCount - unpatchedBefore, "the target is recorded as unpatched once");
        var info = Harmony.GetPatchInfo(method);
        Assert.IsFalse(info.Prefixes.Any(p => p.owner == ForeignOwner), "the foreign owner's prefix must be stripped");
        Assert.IsTrue(info.Finalizers.Any(p => p.owner == ShieldOwner), "the shield's own finalizer must stay");
    }

    [TestMethod]
    public void ShieldFinalizerVoid_ForeignPrefixThrowsMissingMethod_SwallowsLogsAndStripsTheOwner()
    {
        var message = "plan034-" + Guid.NewGuid().ToString("N");
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;
        int unpatchedBefore = PatchShield.UnpatchedCount;

        Targets.SwallowVoid();   // must not throw

        AssertSwallowedLoggedAndStripped(nameof(Targets.SwallowVoid), message, offset, swallowedBefore, unpatchedBefore);
    }

    [TestMethod]
    public void ShieldFinalizerWithResult_ForeignPrefixThrowsMissingMethod_ReturnsDefaultLogsAndStripsTheOwner()
    {
        var message = "plan034-" + Guid.NewGuid().ToString("N");
        ForeignPatches.NextMessage = message;
        long offset = DiagLogLength();
        long swallowedBefore = PatchShield.SwallowedMissingMethod;
        int unpatchedBefore = PatchShield.UnpatchedCount;

        var result = Targets.SwallowWithResult();

        Assert.AreEqual(0, result, "a swallowed call returns default, never the original's 7");
        AssertSwallowedLoggedAndStripped(nameof(Targets.SwallowWithResult), message, offset, swallowedBefore, unpatchedBefore);
    }

    [TestMethod]
    public void ShieldFinalizerVoid_NonTrinityException_MarkerNamesTheShieldedMethod()
    {
        var ex = Capture(Targets.RethrowVoid);

        Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));
        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.RethrowVoid)));
    }

    [TestMethod]
    public void ShieldFinalizerWithResult_NonTrinityException_MarkerNamesTheShieldedMethod()
    {
        var ex = Capture(() => Targets.RethrowWithResult());

        Assert.IsInstanceOfType(ex, typeof(InvalidOperationException));
        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.RethrowWithResult)));
    }

    [TestMethod]
    public void ShieldFinalizer_NestedShieldedMethods_EachMarkerNamesItsOwnMethod()
    {
        // The outer finalizer runs after the inner replacement has unwound, so each must find its own.
        var ex = Capture(Targets.OuterShielded);

        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.InnerShielded)));
        StringAssert.Contains(ex.StackTrace, Marker(nameof(Targets.OuterShielded)));
    }
}
```

The assertions name the original exactly: a lookup that returned the Harmony replacement instead
would print `?` (no declaring type) in the diag.log line, find no patch info (no strip, no
`unpatched` line), and print `..._Patch<N>) ---` in the marker, so each test would fail.

**Verify**: build, then run the class filter `FullyQualifiedName~PatchShieldFinalizerTests`: 5 tests,
all pass, on the unmodified `PatchShield.cs`. If any fails, STOP: this plan's description of today's
behaviour is wrong, and its oracles must not be adjusted to fit.

### Step B2 (Branch B): RED, the finalizers must not bind `__originalMethod`

Add to `PatchShieldFinalizerTests`:

```csharp
    [TestMethod]
    public void ShieldFinalizers_Parameters_TakeOnlyTheException()
    {
        // Plan 034: a finalizer parameter named __originalMethod makes Harmony's wrapper call
        // MethodBase.GetMethodFromHandle on every call of every shielded method (Step 2's benchmark
        // measured <D1> ns per call). The finalizers resolve the shielded method on a throw instead.
        foreach (var name in new[] { "ShieldFinalizerVoid", "ShieldFinalizerWithResult" })
        {
            var method = AccessTools.Method(typeof(PatchShield), name);
            Assert.IsNotNull(method, name);
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length, $"{name}({string.Join(", ", parameters.Select(p => p.Name))})");
            Assert.AreEqual("__exception", parameters[0].Name, name);
            Assert.AreEqual(typeof(Exception), parameters[0].ParameterType, name);
        }
    }
```

Replace `<D1>` with the measured value.

**Verify**: build, run the class filter: 6 tests, 5 pass, and
`ShieldFinalizers_Parameters_TakeOnlyTheException` fails with an `Assert.AreEqual` on the parameter
count (expected 1, actual 2). Quote the failure in your report.

### Step B3 (Branch B): RED, the lookup returns the replacement's original and never an outer method

Add `using System.Reflection.Emit;` to the test file's usings. Then add two targets, a probe class,
their patches and three tests:

```csharp
    // In Targets:
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Probed() => throw new InvalidOperationException("034 probe");
        [MethodImpl(MethodImplOptions.NoInlining)] public static void OuterOfUnknownFrame() => Probe.UnknownFrame!();

    // A nested class beside ForeignPatches:
    public static class Probe
    {
        public static MethodBase? Seen;
        public static MethodBase? SeenFromUnknownFrame;
        public static Action? UnknownFrame;

        public static Exception? Finalizer(Exception __exception)
        {
            if (__exception != null) Seen = PatchShield.ResolveShieldedOriginal(typeof(Probe));
            return null;   // swallow: the probe only records
        }

        public static void Postfix() { }

        // A dynamic method Harmony never built. It stands in for a replacement whose frame does not
        // resolve (one another Harmony copy built, say): it calls the lookup directly, so its own frame is
        // the one judged, and the patched OuterOfUnknownFrame's replacement sits one frame further up.
        // It stores the result instead of returning it: a "call; ret" body lets the x64 JIT make an
        // implicit tail call, which removes this frame from the stack and hides the case under test.
        public static Action BuildUnknownFrame()
        {
            var resolve = AccessTools.Method(typeof(PatchShield), nameof(PatchShield.ResolveShieldedOriginal));
            var dynamicMethod = new DynamicMethod("Plan034UnknownFrame", typeof(void), Type.EmptyTypes,
                typeof(PatchShieldFinalizerTests).Module, skipVisibility: true);
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Ldtoken, typeof(Probe));
            il.Emit(OpCodes.Call, AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle)));
            il.Emit(OpCodes.Call, resolve);
            il.Emit(OpCodes.Stsfld, AccessTools.Field(typeof(Probe), nameof(SeenFromUnknownFrame)));
            il.Emit(OpCodes.Ret);
            return (Action)dynamicMethod.CreateDelegate(typeof(Action));
        }
    }

    // At the end of Init:
        _shield.Patch(Target(nameof(Targets.Probed)),
            finalizer: new HarmonyMethod(AccessTools.Method(typeof(Probe), nameof(Probe.Finalizer))));
        _shield.Patch(Target(nameof(Targets.OuterOfUnknownFrame)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(Probe), nameof(Probe.Postfix))));
        Probe.UnknownFrame = Probe.BuildUnknownFrame();

    [TestMethod]
    public void ResolveShieldedOriginal_CalledFromAFinalizer_ReturnsThePatchedOriginal()
    {
        // The MonoMod-generated replacement is on the stack; Harmony maps it back to the original.
        Probe.Seen = null;

        Targets.Probed();   // throws inside the replacement; the probe swallows

        Assert.IsNotNull(Probe.Seen, "the frame that called the probe did not resolve to a Harmony replacement");
        // Declaring type and name first: a returned replacement has no declaring type, and reading a
        // dynamic method's MethodHandle throws, which would hide what went wrong.
        Assert.AreEqual(typeof(Targets), Probe.Seen!.DeclaringType, $"resolved {Probe.Seen.Name}");
        Assert.AreEqual(nameof(Targets.Probed), Probe.Seen.Name);
        Assert.AreEqual(Target(nameof(Targets.Probed)).MethodHandle, Probe.Seen.MethodHandle);
    }

    [TestMethod]
    public void ResolveShieldedOriginal_CallerIsNotAReplacement_ReturnsNull()
    {
        // Called straight from this test method, which Harmony never built: the judged frame is this one.
        Assert.IsNull(PatchShield.ResolveShieldedOriginal(typeof(Probe)));
    }

    [TestMethod]
    public void ResolveShieldedOriginal_JudgedFrameDoesNotResolve_ReturnsNullNotAnOuterPatchedMethod()
    {
        // A lookup that kept climbing past a frame it could not resolve would return the patched
        // OuterOfUnknownFrame further up the stack, and the swallow path would then strip the foreign
        // owners' patches from that innocent method.
        Probe.SeenFromUnknownFrame = null;

        Targets.OuterOfUnknownFrame();

        var seen = Probe.SeenFromUnknownFrame;
        Assert.IsNull(seen, $"resolved {seen?.DeclaringType?.FullName}.{seen?.Name}");
    }
```

**Verify**: build `TAOM.Tests` (`dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`):
it fails with CS0117, `PatchShield` has no definition for `ResolveShieldedOriginal`, at every
reference (the probe finalizer, the `nameof` in `BuildUnknownFrame`, and the call in
`ResolveShieldedOriginal_CallerIsNotAReplacement_ReturnsNull`). Quote it.

### Step B4 (Branch B): GREEN, resolve the original on the exception path only

In `Dependencies/Foundation/PatchShield.cs`:

1. Add `using System.Runtime.CompilerServices;` (`System.Diagnostics` is already imported).
2. Replace both finalizers (:238-271) with this shape. Each keeps its own direct
   `RethrowStackPreserver.PreserveForRethrow` call: `ShieldFinalizers_EveryRethrowingFinalizer_CallsPreserveForRethrow`
   IL-scans the finalizers themselves, so the call must not move into a helper. Each calls the
   lookup directly and passes `typeof(PatchShield)`, the finalizers' own declaring type.

```csharp
    /// <summary>
    /// Finalizer for void-return methods. Catches the swallow-trinity and returns
    /// silently to suppress the exception; non-matching exceptions are re-thrown by
    /// returning the ORIGINAL exception (Harmony Finalizer convention).
    ///
    /// Harmony calls this on EVERY call of the patched method, with a null exception when
    /// nothing threw, so the no-exception path must stay one null check. It takes no
    /// <c>__originalMethod</c>: that parameter makes Harmony's wrapper call
    /// <c>MethodBase.GetMethodFromHandle</c> on every call (plan 034 measured about <D1> ns), and the
    /// shielded method is needed only after a throw, where <see cref="ResolveShieldedOriginal"/> finds it.
    /// </summary>
    private static Exception? ShieldFinalizerVoid(Exception __exception)
    {
        if (__exception == null) return null;
        var originalMethod = ResolveShieldedOriginal(typeof(PatchShield));
        if (ShouldSwallow(originalMethod, __exception)) return null;

        // (keep the existing four-line comment about Harmony's `throw` and bundle 2d446100 here)
        Interlocked.Increment(ref _rethrown);
        return RethrowStackPreserver.PreserveForRethrow(__exception, originalMethod);
    }

    // ShieldFinalizerWithResult: keep its existing summary, add the same "It takes no
    // __originalMethod" sentence, and give it the identical body (its existing one-line
    // "Same rethrow as ShieldFinalizerVoid; see there." comment stays).
```

3. Change the parameter type of `ShouldSwallow` and `TryUnpatchOffendingPatches` from `MethodBase` to
   `MethodBase?` (both already handle null: `?.` at :299-300 and the early return at :317). No other
   change to either method.
4. **RED for the bound.** Add the lookup below the finalizers, first with its last loop line in the
   climbing form marked `RED variant` below:

```csharp
    /// <summary>
    /// The shielded method whose Harmony replacement called the finalizer that calls this, or null.
    /// Call it directly from the finalizer and pass the finalizer's declaring type. It skips the frames
    /// of that type (the finalizer itself, unless the JIT inlined it into the replacement) and judges
    /// exactly one frame, the next one: the replacement running the finalizer. It returns that frame's
    /// original when Harmony maps it back (<see cref="Harmony.GetOriginalMethodFromStackframe"/>), and
    /// otherwise null. It never climbs further, because an outer shielded method on the same stack is
    /// the wrong method, and the swallow path would unpatch it. Exception path only: a stack walk costs
    /// far more than a call, and the finalizers reach this only after a throw. The callers already
    /// handle null (the log names "?", nothing is unpatched, and the rethrow marker says "an unknown
    /// method"). Never throws.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static MethodBase? ResolveShieldedOriginal(Type finalizerType)
    {
        try
        {
            var frames = new StackTrace(1, false).GetFrames();
            if (frames == null) return null;
            foreach (var frame in frames)
            {
                var method = frame?.GetMethod();
                if (method == null) return null;
                if (method.DeclaringType == finalizerType) continue;   // the calling finalizer's own frame
                var original = Harmony.GetOriginalMethodFromStackframe(frame);
                if (original != null && !ReferenceEquals(original, method)) return original;   // RED variant
            }
        }
        catch
        {
            // Fail open: a null original degrades the log line and the unpatch, never the swallow.
        }
        return null;
    }
```

   Build, then run the class filter. **Verify**: 9 tests run, 8 pass, and
   `ResolveShieldedOriginal_JudgedFrameDoesNotResolve_ReturnsNullNotAnOuterPatchedMethod` fails on
   `Assert.IsNull` with a message naming `...PatchShieldFinalizerTests+Targets.OuterOfUnknownFrame`:
   the climbing walk returned the outer method. Quote the failure. If that test passes here, STOP: it
   does not exercise the bound.
5. **GREEN.** Replace the `RED variant` line with the bounded line (and drop the `RED variant`
   comment):

```csharp
                return original != null && !ReferenceEquals(original, method) ? original : null;
```

(`GetRealMethod` returns the frame's own method object when the frame is not a replacement, so
`ReferenceEquals` separates the two; Step B3's tests are the proof, not this reasoning.)

**Verify**: build exit 0; the class filter runs 9 tests, all pass; the `RethrowStackPreserverTests`
filter passes with the same count as before your edits and that file unchanged
(`git diff --stat -- TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs` prints
nothing). If `ResolveShieldedOriginal_CalledFromAFinalizer_ReturnsThePatchedOriginal` still fails
after one reasonable fix inside `ResolveShieldedOriginal`, STOP: the lookup is unproven, and no
fallback to `__originalMethod` or to the exception's own trace is allowed. No fix may make the lookup
judge more than the one frame after the caller's own frames: a fix that passes the probe test by
climbing further fails the bound test, and that is a STOP, not a reason to loosen it.

### Step B5 (Branch B): correct the figure and retire the per-call claims

1. Do Step A1's edits (the figure, in all six "both branches" files), with one change to its draft
   wording: where a sentence says PatchShield's finalizer binds `__originalMethod` in the present
   tense, make it past tense and say plan 034 removed it.
2. Rewrite each row of the "present-tense statements" table in Current state so it says what is true
   now: PatchShield's finalizer bound `__originalMethod` until plan 034; it now takes only
   `__exception`; its remaining per-call cost is the finalizer call plus the wrapper's try/catch,
   about `<C>` ns per call against `<A>` ns for a patched method without a finalizer (Step 2's rows c
   and a). Keep every exclusion's history and keep each decision as it stands: the exclusions and the
   co-op skip were decided under the old cost, and whether to revisit them is the maintainer's call.
   Do not change any code line, only comments and prose. On `PatchShield.cs:119-123` the log string
   at :124-126 stays as it is. On `harmony-patch-registry.md:1069` change only the closing clause
   ("so none carries an `__originalMethod` finalizer and its `GetMethodFromHandle` on every call"),
   for example to "so none carries a shield finalizer on its per-unit calls". Word every rewrite
   with "bound" or "took" (past tense), never "binds", "binding tax" or "finalizer tax", so the
   stale-claim grep below stays mechanical.
3. Append one lesson at the end of `docs/reviews/lessons/harmony-il.md`, after a blank line, in the
   file's house shape (draft; re-verify every number against `result-1.txt`):

```markdown
### A per-call cost needs a per-call measurement; an incident's average is not one (2026-10)
From #331's round-2 fix (July 2026) until plan 034, PatchShield's comments, two lessons and three docs priced binding `__originalMethod` at about fifty microseconds per call. That figure was #331's 105 to 109 s tournament-exit stall divided by an estimated call count, on a workload that also ran 8,276 gen0 collections. A benchmark on .NET Framework 4.8.1 with Harmony 2.4.2 (plan 034) measured the binding at about <D1> ns per call over a finalizer that takes only `__exception`, <D2> ns with threads contending, and <BYTES> bytes per call. PatchShield's finalizers now take only `__exception` and find the shielded method from the stack after a throw (`PatchShield.ResolveShieldedOriginal`).
- **Why missed:** the number came from the RCA that found the mechanism, and every later document copied it as a per-call constant.
- **Prevent:** a cost figure that justifies a design choice says how it was measured. A Harmony patch that needs the original method only on a rare path finds it there with `Harmony.GetOriginalMethodFromStackframe`, judging only the frame that called the patch method and never climbing to an outer one, instead of binding `__originalMethod` for every call.
- **Source:** plan 034 (benchmark in its commit body); `PatchShieldFinalizerTests`.
```

"July 2026" comes from `PatchShieldPolicy.cs:67` ("Issue #331 round 2 (2026-07-09, measured)");
re-read that line before you keep it.

**Verify**:
- the Step A1 grep returns nothing;
- the stale-claim grep from Current state ("The stale-claim grep") prints exactly 6 lines (19 at
  `dffdf879`), and they are the six that stay, one each in `Dependencies/Foundation/PatchShield.cs`
  (the log string), `docs/features/bannerlord-together-compat.md`, `docs/features/crash-report.md`,
  `docs/research/bannerlordcoop-internals.md`, and two in `docs/reviews/lessons/harmony-il.md` (the
  line starting "A Harmony patch that binds" and the one holding "took its finalizer, which binds").
  Any other line is a present-tense claim you have not retired: reword it as step 2 says;
- `python tools/lint_docs.py --fail-on-drift` exits 0;
- your edits add no em or en dash: the count from
  `git diff -U0 | LC_ALL=C.UTF-8 grep -P "^\+(?!\+\+)" | LC_ALL=C.UTF-8 grep -o -P "[\x{2013}\x{2014}]" | wc -l`
  is not larger than the count from the same command with `^-(?!--)` in place of `^\+(?!\+\+)`
  (without `LC_ALL=C.UTF-8`, Git Bash's grep rejects the pattern). Several edited lines are long
  paragraphs that already hold dashes elsewhere; those may stay, since you are not rewriting them.

### Step B6 (Branch B): full suite, build, commit

**Verify**: build exit 0; the full suite prints
`Failed! - Failed: 1, Passed: 12354, Skipped: 2, Total: 12357` (the baseline plus 9 new tests, the
same single failure), or Step 1's totals plus 9 passing if Step 1 differed; `git status --porcelain`
lists only the 14 Branch B scope files. Commit them with the Branch B subject. The body explains, for
a release-note reader: every method PatchShield guards paid a reflection lookup on every call to know
its own name in case of an error; the shield now looks the name up only when an error happens; what
it still does after an error is unchanged; and the measured numbers.

## Test plan

- Branch A adds no test: it changes comments and docs only, and its evidence is Step 2's output.
- Branch B, new file `TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs`, 9 tests:
  - swallow path, void and with-result: the swallow, the default return, the counter delta, the exact
    diag.log line, the exact `unpatched owner` line keyed by the original's module and token, the
    `UnpatchedCount` delta, the foreign prefix stripped and the shield kept (2 tests);
  - rethrow path, void and with-result: the marker names the original exactly (2 tests);
  - nested shielded methods: each marker names its own method (1 test);
  - the finalizers take only `Exception __exception` (1 test, RED at base);
  - `ResolveShieldedOriginal` from a finalizer returns the original, and called from a frame that is
    not a replacement returns null (2 tests, RED at base by CS0117);
  - the bound: when the judged frame does not resolve, the lookup returns null and never the patched
    method further up the stack (1 test, RED at base by CS0117 and RED again against the climbing
    variant in Step B4).
- Pattern: `TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs`.
- Unchanged and still green: `RethrowStackPreserverTests` (the rethrow contract), `PatchShieldPolicyTests`.
- Not testable here, for the `Not-tested:` trailer: the frame-time effect in a live battle, and a
  replacement built by a different Harmony copy than the one the tests load.

## Done criteria

ALL must hold:

- [ ] `result-1.txt` (and `result-2.txt` if Step 3 needed it) passes every Step 2 check, and your
      report quotes the table and the three delta lines.
- [ ] The build command exits 0.
- [ ] Branch A: the test totals equal Step 1's. Branch B: Step 1's totals plus 9 new passing tests,
      `PatchShieldFinalizerTests` 9 of 9, `RethrowStackPreserverTests` all passing, and your report
      quotes Step B4's RED failure of the bound test against the climbing variant.
- [ ] `git grep -n -F -e "50µs" -e "50 µs" -e "50μs" -e "50 μs" -e "50 microsecond" -- Dependencies TAOM.Tests docs/features docs/migration docs/reference docs/reviews/lessons`
      returns nothing.
- [ ] Branch B: `git grep -n -E "MethodBase\??\s+__originalMethod" -- Dependencies/Foundation/PatchShield.cs`
      returns nothing (at `dffdf879` it prints the two finalizer declarations, :246 and :264), the
      stale-claim grep prints exactly the six lines Step B5 names, and
      `git diff --stat -- Dependencies/Foundation/RethrowStackPreserver.cs TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs Dependencies/Foundation/SaveShield.cs`
      prints nothing.
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0.
- [ ] `git status --porcelain` lists only in-scope files for the branch taken.
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes and against `result-1.txt`.

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts.
- The benchmark does not run on .NET Framework 4.8 x64 with Harmony 2.4.2.0, optimized and without a
  debugger, or its sums mismatch (Step 2).
- Step 3's two runs fall on different sides of 50 ns.
- Any Step B1 characterization test fails on the unmodified code.
- `ResolveShieldedOriginal_CalledFromAFinalizer_ReturnsThePatchedOriginal` fails after one reasonable
  fix: the exception-path lookup cannot be proven to return the original.
- The lookup returns a method other than the one whose replacement called the finalizer: the bound
  test fails after Step B4's GREEN, or it passes against the climbing RED variant (then it does not
  exercise the bound). Never make the lookup judge more than the one frame after its caller's own
  frames to get a test green.
- Making Branch B pass would change which exceptions are swallowed or rethrown, the diag.log text, the
  unpatch bookkeeping, or the rethrown stack trace; or would need any edit to `RethrowStackPreserver.cs`,
  `RethrowStackPreserverTests.cs`, `SaveShield.cs`, the exclusion lists or `ShouldInstall`.
- The fix seems to need an out-of-scope file, especially `IoC.cs`, `SubModule.cs`, a csproj or a
  protected file.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (perf, crash-safety). No issue number is known at planning time.
- `/deep-review` of the branch before merge (C# changes in Branch B; comment-only C# in Branch A).
- No `/localize`: no player-facing text. No new feature doc or feature-map row: PatchShield's row
  (`docs/reference/feature-map.md:112`, "TAOM.Dependencies defensive infrastructure") is unchanged.
- The `bannerlord-1.4.5` line carries the same two finalizers
  (`git show bannerlord-1.4.5:Dependencies/Foundation/PatchShield.cs`, :244 and :255 at its tip on
  2026-10-02); decide whether Branch B is ported there.

## After merge: the maintainer's actions

- Branch A: none.
- Branch B: one battle smoke on a deployed build: diag.log shows the usual `shield pass` lines and no
  new `CAUGHT` line from `PatchShield`. Then, separately, decide whether the cheaper finalizer changes
  the exclusion lists or the co-op skip (out of this plan's scope).

## Maintenance notes

- Branch B moves a stack walk onto the exception path. A modlist whose exceptions cross a shielded
  method often (a non-trinity exception caught higher up) now pays it per crossing, on top of the
  `new StackTrace(exception)` that `RethrowStackPreserver` already builds. The session summary's
  `rethrew N` count shows how often that happens; review should probe whether that cost is acceptable.
- The lookup depends on Harmony's shared `originals` map (`HarmonySharedState`). A Harmony upgrade
  must keep `ResolveShieldedOriginal_CalledFromAFinalizer_ReturnsThePatchedOriginal` green; on Mono the
  map's fallback differs (`methodAddressRef`), which the game does not use.
- The lookup is bounded on purpose: it skips its caller's own frames (by the declaring type the
  caller passes) and judges exactly one frame, the replacement running the finalizer. When that frame
  does not resolve (a replacement another Harmony copy built with a private map, or a frame whose
  method is not a `MethodInfo`), it returns null, the log says "?" and nothing is unpatched. A walk
  that climbed further would hand an OUTER shielded method to `TryUnpatchOffendingPatches`, which
  strips foreign patches process-wide. `ResolveShieldedOriginal_JudgedFrameDoesNotResolve_ReturnsNullNotAnOuterPatchedMethod`
  pins this; review should reject any change that loosens it. Callers must call the lookup directly
  from the finalizer (a helper in between, declared on another type, would be judged as the frame).
- The x64 JIT can turn a call in tail position ("call; ret") into an implicit tail call, which drops
  the caller's frame from the stack; the bound test's dynamic method stores its result for that
  reason. The replacement calls finalizers from inside its exception handling, where no tail call is
  made, and the finalizers use the lookup's result, so production frames are not affected.
- `RethrowStackPreserver.cs:60` (out of scope) still documents usage as
  `PreserveForRethrow(__exception, __originalMethod)`, and so do the generic finalizer examples at
  `docs/reference/engine/submodule-lifecycle-and-harmony.md:35` and `docs/reviews/lessons/harmony-il.md:581`.
  They describe any finalizer, SaveShield's included, and stay correct; only PatchShield's own
  finalizers no longer match that example after Branch B. A later doc pass may add "or the method
  from `ResolveShieldedOriginal`".
- Constructor targets: the tests patch methods only. A shielded constructor's replacement is still a
  generated method mapped to its `ConstructorInfo` original in the same map; review may ask for a
  constructor case.
- `SaveShield` and the SaveLoadDiagnostics finalizers still bind `__originalMethod`; their targets run
  per save or load, so the cost there is not material. Left alone deliberately.
- The benchmark measures warm reflection caches. #331's stall also involved heavy gen0 collection, so
  an in-game cost can exceed the isolated figure; the docs say "in isolation" for that reason.
