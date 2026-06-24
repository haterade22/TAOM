You are an adversarial code reviewer. Target: a new TAOM feature, PartyIconScale (Patch53). Your job is to find real bugs, refute or confirm specific hypotheses, and avoid false positives. The installed Bannerlord engine is v1.4.6. TAOM is a .NET Framework 4.7.2 Bannerlord total-conversion mod. Project rules are in AGENTS.md (read automatically).

FEATURE SUMMARY
A Harmony transpiler rewrites the two hardcoded 0.3f scale literals inside the private method SandBox.View.Map.Visuals.MobilePartyVisual.AddCharacterToPartyIcon (in SandBox.View.dll) into a call to the static method PartyIconScaleConfig.GetScale(), so campaign-map party-icon figures (the leader figure and its mount) honour an MCM slider "Map Figure Scale" (default 0.15, range 0.05 to 1.0) instead of the engine constant 0.3. People site = ldc.r4 0.3 immediately before callvirt AgentVisualsData::Scale. Mount site = ldc.r4 0.3 immediately before mul (the IL of item.ScaleFactor * 0.3f). The swap mutates the ldc.r4 instruction in place to a call. GetScale reads the live MCM value each invocation and validates it. Coexists with an existing BannerColorPersistence Postfix on the same method.

READ FIRST
- docs/features/party-icon-scale.md
- Main/Features/PartyIconScale/PartyIconScaleConfig.cs
- Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs
- Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.cs
- Main/Features/TaomSettings.cs (only the MapFigureScale property, group "Map UI/Party Icons" -- grep MapFigureScale)
- Main/SubModule.cs (only the Patch53 registration -- grep Patch53)
- TAOM.Tests/Features/PartyIconScale/PartyIconScaleConfigTests.cs
- TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs
- Main/Features/CastleRecruitment/Hooks/CastleAiToggle.cs and CastleAiTranspiler.cs (the static-IL-call-target precedent this feature follows)
- Main/Features/BannerColorPersistence/Hooks/MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs (the coexisting Postfix on the same method)
- Main/Core/Validation/FiniteFloatValidator.cs

VANILLA IL (verified from installed v1.4.6 SandBox.View.dll via ilspycmd -il on MobilePartyVisual.AddCharacterToPartyIcon). There are exactly three ldc.r4 0.3 in the method body:

PEOPLE site (leader figure):
IL_0116: ldc.r4 0.3
IL_011b: callvirt instance class TaleWorlds.MountAndBlade.AgentVisualsData TaleWorlds.MountAndBlade.AgentVisualsData::Scale(float32)

MOUNT site (item.ScaleFactor * 0.3f):
IL_036b: callvirt instance float32 TaleWorlds.Core.ItemObject::get_ScaleFactor()
IL_0370: ldc.r4 0.3
IL_0375: mul
IL_0376: callvirt instance class TaleWorlds.MountAndBlade.AgentVisualsData TaleWorlds.MountAndBlade.AgentVisualsData::Scale(float32)

THIRD 0.3 (animation-speed math, must NOT be matched):
IL_04f1: mul
IL_04f2: ldc.r4 0.3
IL_04f7: div

AgentVisualsData.Scale is: public AgentVisualsData Scale(float scale). GetScale is: public static float GetScale(). AddCharacterToPartyIcon has exactly one overload (private instance).

KNOWN SUSPECTS -- CONFIRM or REFUTE each with concrete reasoning. Do not rubber-stamp.

S1. IL adjacency robustness. The transpiler matches the FIRST ldc.r4 with operand == 0.3f whose NEXT instruction (a) is a Call/Callvirt to a MethodInfo named "Scale" (people), or (b) is OpCodes.Mul (mount), and swaps that ldc to a call. Two separate first-match scans, one per pattern. Could either scan ever (i) grab the wrong instruction within this method, (ii) match the third 0.3 (the ->div anim-math literal), (iii) be defeated by the JIT/Harmony presenting the literal as something other than OpCodes.Ldc_R4 with a boxed float operand, or (iv) match in a sibling method? Note the transpiler is attached ONLY to AddCharacterToPartyIcon, not AddMountToPartyIcon. Verify the "Scale" name-only match cannot collide with another method named Scale on a different type at the people site.

S2. Float literal equality. The match condition is `operand is float f && f == 0.3f`. Is exact == reliable here, given the IL literal is itself emitted from the float constant 0.3f (same bit pattern) and Harmony stores ldc.r4 operands as a boxed System.Single? Could the operand ever be a double, or a float whose bits differ from the C# literal 0.3f? Is there any culture/rounding risk? Refute or confirm that == is correct and that an epsilon compare is NOT needed (and would in fact be wrong by also matching 0.325 etc).

S3. In-place mutation and labels/exception blocks. The swap does `list[i].opcode = OpCodes.Call; list[i].operand = getScale;` on the existing CodeInstruction object (not a new one). Confirm this preserves the instruction's labels and exception-handler blocks (branch targets / try-catch boundaries that may point at the ldc). Is there any case where the ldc.r4 0.3 is itself a branch target such that replacing the opcode in place is unsafe? Compare to how CastleAiTranspiler does its in-place swap.

S4. GetScale null-safety and bounds. GetScale returns Resolve(TaomSettings.Instance?.MapFigureScale). Resolve(float? raw) returns raw if `raw is float v && FiniteFloatValidator.IsFiniteInRange(v, Min, Max)` else Default (0.15). Min=0.05, Max=1.0. The MCM SettingPropertyFloatingInteger slider range is 0.05 to 1.0. Confirm the slider bounds and the validator bounds match exactly. Identify any value the slider can produce that Resolve would reject-to-default (would be a silent UX surprise), or any value Resolve accepts that the slider cannot produce. Confirm TaomSettings.Instance == null (main menu / custom battle / before MCM load) yields 0.15 not a crash. Is reading TaomSettings.Instance on every icon rebuild a correctness risk (e.g., MCM returns a partially-initialized instance)?

S5. Transpiler idempotency / re-apply. If the Patch53 PatchCategory were ever applied twice (e.g., a second game session, OnGameInitializationFinished re-entry), what happens? The transpiler is a pure function over the instruction stream; after the first apply the ldc.r4 0.3 sites no longer exist (they are Call). On a second apply over the ALREADY-PATCHED method body, would Rewrite find zero sites and merely log warnings (fail-safe), or could it throw / double-rewrite / corrupt the stream? Confirm Rewrite never throws on a missing site. Separately: is Patch53 applied once (PatchCategory in OnSubModuleLoad) or per-game? Check SubModule.cs.

S6. Coexistence with the BannerColorPersistence Postfix on the same method. A transpiler (Patch53) and a Postfix (BannerColorPersistence) both target MobilePartyVisual.AddCharacterToPartyIcon. Confirm Harmony composes these without conflict (transpiler rewrites the body; postfix wraps). Is there any ordering or registration concern (both use category patching)? Could the transpiler's IL changes break the postfix's ref-parameter access (teamColor1/teamColor2)? The postfix reads CharacterObject + ref uint teamColor1/teamColor2 -- the transpiler does not touch those.

ADDITIONAL DEEP CHECKS
- Standards: TAOM uses the adapter pattern (ADR-007) for TaleWorlds SEALED types in services. Is PartyIconScaleConfig.GetScale reading TaomSettings.Instance directly a violation? Note: TaomSettings is a TAOM-owned MCM class (AttributeGlobalSettings<TaomSettings>), not a TaleWorlds engine type. The IL call target MUST be static (no `this` available at the rewritten call site). CastleAiToggle is the accepted precedent: a public static class whose static method is the transpiler's call target, which itself dereferences the genuinely-sealed TaleWorlds Settlement type. State whether you agree GetScale is NOT an ADR-007 violation, or argue concretely why it is.
- Is a one-implementation IPartyIconScaleService + IoC registration warranted here, or is that YAGNI ceremony given Resolve is already a pure, fully-unit-tested function and the call target must be static? TAOM has a simplicity-criterion rule (reject new abstractions for tiny wins). Give a concrete verdict.
- Test gaps: PartyIconScaleConfigTests covers Resolve (valid/null/NaN/+Inf/-Inf/below-min/above-max/min-boundary/max-boundary/default). PartyIconScaleTranspilerTests covers people-swap, mount-swap, both-with-decoys (0.325 and 0.3-before-Div untouched), label preservation, null-getScale fail-safe, missing-people-site-still-swaps-mount. Identify any uncovered behavior that a real bug could hide in (e.g., a stream with the mount site BEFORE the people site; a stream where the people 0.3 appears but next is Call (not Callvirt) to Scale; multiple Scale calls).
- Dead code / unused: anything declared but never consumed?

REQUIRED OUTPUT SECTIONS
1. KNOWN SUSPECTS VERDICTS -- S1..S6, each CONFIRMED (real issue) / REFUTED (not an issue) / UNCERTAIN, with the specific code/IL reasoning that drove the verdict.
2. FINDINGS -- each with severity (P1 critical / P2 major / P3 minor / NIT), file:line, what is wrong, why it matters, and the minimal fix. If none, say so explicitly per area.
3. THINGS THE CLAUDE DEEP-REVIEW MAY HAVE MISSED -- anything the 5-agent pass would not have caught.
4. QUALITY GATE -- READY TO COMMIT / NEEDS FIXES, one line.

QUALITY GATES
- Do not flag vanilla-matching behavior as a bug.
- Do not invent IDs; this feature has no kingdom/culture/troop config.
- Cite file:line for every finding.
- If you claim something is "missing," grep first -- state that you grepped.
- Prefer refuting your own hypotheses with evidence over asserting them.

Write your full review as the response.
