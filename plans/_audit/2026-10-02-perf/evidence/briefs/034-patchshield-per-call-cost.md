Plan 034: measure what PatchShield's finalizer costs per call of every method it wraps, and if it is
material, make the no-exception path free while keeping every crash-safety behaviour.

FACTS (re-verify each):
- Dependencies/Foundation/PatchShield.cs (TAOM.Dependencies, a separate assembly loaded first): Install
  (:113-236) walks Harmony.GetAllPatchedMethods, skips methods declared in TAOM* assemblies (:165-176) and
  the exclusion lists (:184-189), and attaches a finalizer (:209-210). Both finalizers,
  ShieldFinalizerVoid (:246) and ShieldFinalizerWithResult (:264), take `MethodBase __originalMethod`. The
  original is used only on the exception path: ShouldSwallow's DiagLog line and
  TryUnpatchOffendingPatches (:272-306), and RethrowStackPreserver.PreserveForRethrow.
- The bundled Harmony emits `ldtoken` plus `MethodBase.GetMethodFromHandle` to supply __originalMethod on
  every call of the patched method (E:/Decompiled_Bannerlord/_modules_build/TAOM.Dependencies__0Harmony.cs:4397-4416,
  :4615; verify against the Harmony version TAOM actually ships).
- Coverage: diag.log shows 159 methods wrapped at the first game start of a process and 303 to 305 at the
  second (Modules/TAOM.Dependencies/diag.log "shield pass" lines). Several wrapped targets run per frame,
  per hit, or about twice a second per AI agent on worker threads (Formation.GetOrderPositionOfUnit,
  CommonAIComponent.OnHit, Agent.HandleBlowAux, Agent.CheckToDropFlaggedItem, Formation.SetMovementOrder,
  Mission.Tick and OnTick, Mission.TickAgentsAndTeamsImp, MissionState.TickMissionAux and OnTick,
  MBTextManager.GetLocalizedText). Exclusions exist for Patch92's Formation members, Patch93's wield
  getters, Mission.CanAgentRout and the ManagedCallbacks namespace (PatchShieldPolicy.cs:77-148).
- The "~50 us per call" figure in PatchShieldPolicy.cs:67-70 and docs/reviews/lessons/harmony-il.md:6 is
  not a per-call measurement: it is #331's 104 to 109 s exit stall averaged over about 2 million calls on
  the maintainer's desktop while it ran in the slow mixed-debugger mode; plans/_audit/2026-09-23-opus/followup-patchshield.md:239-240
  lists the per-call cost as UNMEASURED.

STEPS:
1. Measure, on this machine's .NET Framework 4.8.1 (the game's runtime), in an optimized build: the cost
   of a Harmony-patched trivial method with (a) no finalizer, (b) a finalizer taking __originalMethod and
   (c) a finalizer taking only __exception, per call, averaged over enough calls to be stable. Put the
   harness in TAOM.Tests as a test in an explicit benchmark category that is excluded from the default run
   (check how the suite excludes categories; the RequiresGame and BindingVerification categories are the
   shape) or in a scratch console project outside the repo; record the method and the numbers in the plan's
   report and in the commit body.
2. Decide by the numbers: if (b) minus (c) is under 50 ns per call, stop after recording the measurement
   in docs (PatchShield's comments, harmony-il.md lesson line correction, the registry) and correct the
   "~50 us" claim. If it is larger, change both finalizers to take only __exception and resolve the
   original method on the exception path (Harmony.GetOriginalMethodFromStackframe on the frame that
   threw, or an equivalent the bundled Harmony provides; prove it returns the patched original for a
   MonoMod-generated replacement on .NET Framework in a unit test that patches a test method, throws a
   MissingMethodException from a prefix, and asserts the swallow, the DiagLog text and the unpatch
   bookkeeping are unchanged).
3. Either way, correct the claim text wherever it appears (grep "50 ?us", "50µs", "~50").

OUT OF SCOPE: the exclusion lists (a separate maintainer decision about crash-safety coverage; FOR-MIKE),
which methods PatchShield wraps, its pass timing.

STOP conditions to include: the exception-path lookup cannot be proven to return the original in a test;
any change to which exceptions are swallowed or rethrown, or to the rethrown stack trace (the
RethrowStackPreserver contract and its tests must stay green unchanged).
