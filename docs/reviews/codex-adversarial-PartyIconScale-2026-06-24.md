OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019efa0b-0c4b-75b2-94c6-bfe054cbab55
--------
user
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

ERROR: The process "63460" not found.
codex
I’ll review this as a read-only pass: first the feature files and tests, then the patch registration and sibling Harmony precedent, then I’ll verify the specific IL hypotheses against the implementation.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "''^" Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "''^" Main/Features/PartyIconScale/PartyIconScaleConfig.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "''^" docs/features/party-icon-scale.md' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "''^" Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "MapFigureScale|Map UI|Party Icons" Main/Features/TaomSettings.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "Patch53|PartyIconScale" Main/SubModule.cs' in C:\Users\mikew\source\repos\TAOM
2026-06-24T14:31:56.276809Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.6 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" Main/Features/PartyIconScale/PartyIconScaleConfig.cs[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-24T14:31:56.276808Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.6 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" docs/features/party-icon-scale.md[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2612ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" Main/Features/PartyIconScale/PartyIconScaleConfig.cs[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2612ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" docs/features/party-icon-scale.md[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2601ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "MapFigureScale|Map UI|Party Icons" Main/Features/TaomSettings. …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-24T14:31:56.281335Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.6 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "MapFigureScale|Map UI|Party Icons" Main/Features/TaomSettings. …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-24T14:31:56.287067Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.6 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "Patch53|PartyIconScale" Main/SubModule.cs[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2600ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "Patch53|PartyIconScale" Main/SubModule.cs[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2602ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.c …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-24T14:31:56.287644Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.6 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.c …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-24T14:31:56.291587Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.6 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2612ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "^" Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not installed in this environment, so I’m switching to PowerShell-native reads with line numbers. I’ll keep the pass read-only.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/Features/PartyIconScale/PartyIconScaleConfig.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'docs/features/party-icon-scale.md' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/Features/TaomSettings.cs' -Pattern 'MapFigureScale|Map UI|Party Icons' -Context 3,6" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/SubModule.cs' -Pattern 'Patch53|PartyIconScale' -Context 3,3" in C:\Users\mikew\source\repos\TAOM
 succeeded in 621ms:
1:using TAOM.Core.Validation;
2:
3:namespace TAOM.Features.PartyIconScale;
4:
5:/// <summary>
6:/// Resolves the campaign-map party-icon figure/mount scale from the MCM "Map Figure Scale" slider
7:/// (<see cref="TaomSettings.MapFigureScale"/>).
8:/// <para>
9:/// <see cref="GetScale"/> is the static that the <c>Patch53_PartyIconScale</c> transpiler rewrites the
10:/// hardcoded vanilla <c>0.3f</c> literal in <c>MobilePartyVisual.AddCharacterToPartyIcon</c> into a
11:/// <c>call</c> of. It MUST stay a public, parameterless, <see cref="float"/>-returning static so the IL
12:/// <c>call</c> stays stack-neutral with the <c>ldc.r4</c> it replaces. <see cref="Resolve"/> holds the
13:/// pure, validated logic (tested via <c>InternalsVisibleTo("TAOM.Tests")</c>).
14:/// </para>
15:/// <para>
16:/// Validation falls back to <see cref="Default"/> for NaN / ±Infinity / out-of-range / null per the
17:/// "Config Providers MUST Validate" rule. It deliberately does NOT log on fallback: the MCM slider already
18:/// clamps user input to [<see cref="Min"/>, <see cref="Max"/>], so an invalid value is only reachable via a
19:/// hand-corrupted settings JSON, and <see cref="GetScale"/> runs on every party-icon rebuild — per-call
20:/// warning logging would spam the log for a value the UI already guards.
21:/// </para>
22:/// </summary>
23:public static class PartyIconScaleConfig
24:{
25:    /// <summary>Default map-figure scale — half of vanilla's 0.3.</summary>
26:    public const float Default = 0.15f;
27:
28:    /// <summary>Smallest allowed scale; below this figures are effectively invisible on the map.</summary>
29:    public const float Min = 0.05f;
30:
31:    /// <summary>Largest allowed scale; 1.0 lets a user make figures larger than vanilla (0.3) for testing.</summary>
32:    public const float Max = 1.0f;
33:
34:    /// <summary>
35:    /// Validates a raw slider value. Returns it when finite and within [<see cref="Min"/>, <see cref="Max"/>];
36:    /// otherwise (NaN / ±Infinity / out-of-range / null) returns <see cref="Default"/>.
37:    /// </summary>
38:    internal static float Resolve(float? raw) =>
39:        raw is float v && FiniteFloatValidator.IsFiniteInRange(v, Min, Max) ? v : Default;
40:
41:    /// <summary>
42:    /// The map-figure scale the engine's party-icon builder uses. Invoked from the rewritten IL of
43:    /// <c>MobilePartyVisual.AddCharacterToPartyIcon</c> (people site directly, mount site as
44:    /// <c>ScaleFactor * GetScale()</c>). Reads the live MCM value so a slider change applies on the next
45:    /// icon rebuild; null-safe when MCM/settings aren't loaded (main menu, custom battle, tests).
46:    /// </summary>
47:    public static float GetScale() => Resolve(TaomSettings.Instance?.MapFigureScale);
48:}

 succeeded in 613ms:
1:using System;
2:using System.Collections.Generic;
3:using System.Reflection;
4:using System.Reflection.Emit;
5:using HarmonyLib;
6:using TAOM.Core.Logging;
7:
8:namespace TAOM.Features.PartyIconScale;
9:
10:/// <summary>
11:/// IL surgery for <c>Patch53_PartyIconScale</c>. Rewrites the two hardcoded <c>0.3f</c> scale literals in
12:/// <c>MobilePartyVisual.AddCharacterToPartyIcon</c> into <c>call PartyIconScaleConfig.GetScale()</c> so the
13:/// campaign-map party-icon figure and its mount honour the MCM "Map Figure Scale" slider.
14:/// <list type="bullet">
15:///   <item><b>People:</b> <c>ldc.r4 0.3</c> immediately before <c>callvirt AgentVisualsData::Scale</c>
16:///   (<c>.Scale(0.3f)</c>).</item>
17:///   <item><b>Mount:</b> <c>ldc.r4 0.3</c> immediately before <c>mul</c>
18:///   (<c>.Scale(item.ScaleFactor * 0.3f)</c>).</item>
19:/// </list>
20:/// Both swaps are stack-neutral: <c>ldc.r4</c> and the parameterless <c>float GetScale()</c> each push one
21:/// float and pop none. The <c>ldc.r4</c> is mutated <i>in place</i> so any branch labels/exception blocks on
22:/// it survive. The other <c>0.3</c> literals in the method feed animation-speed math (<c>… / 0.3f</c> =
23:/// <c>div</c>) and are not matched. If either site is absent (engine refactor), the swap is skipped with a
24:/// warning and vanilla <c>0.3</c> is preserved — never throw, so a category re-apply can't crash.
25:/// </summary>
26:internal static class PartyIconScaleTranspiler
27:{
28:    internal static List<CodeInstruction> Rewrite(
29:        IEnumerable<CodeInstruction> instructions, MethodInfo? getScale, IModLogger? logger)
30:    {
31:        var list = new List<CodeInstruction>(instructions);
32:
33:        if (getScale == null)
34:        {
35:            logger?.LogWarning(
36:                "[PartyIconScale] GetScale lookup failed — party-icon scale not patched (vanilla 0.3 preserved)");
37:            return list;
38:        }
39:
40:        if (!TrySwapZeroPointThree(list, getScale, NextIsScaleCall))
41:            logger?.LogWarning(
42:                "[PartyIconScale] people scale site (ldc.r4 0.3 → callvirt Scale) not found — left vanilla");
43:
44:        if (!TrySwapZeroPointThree(list, getScale, NextIsMul))
45:            logger?.LogWarning(
46:                "[PartyIconScale] mount scale site (ldc.r4 0.3 → mul) not found — left vanilla");
47:
48:        return list;
49:    }
50:
51:    // Finds the first `ldc.r4 0.3` whose following instruction matches <paramref name="nextMatches"/> and
52:    // replaces it in place with `call getScale`. Returns false if no such site exists.
53:    private static bool TrySwapZeroPointThree(
54:        List<CodeInstruction> list, MethodInfo getScale, Func<CodeInstruction, bool> nextMatches)
55:    {
56:        for (int i = 0; i < list.Count - 1; i++)
57:        {
58:            if (list[i].opcode == OpCodes.Ldc_R4
59:                && list[i].operand is float f && f == 0.3f
60:                && nextMatches(list[i + 1]))
61:            {
62:                list[i].opcode = OpCodes.Call;
63:                list[i].operand = getScale;
64:                return true;
65:            }
66:        }
67:        return false;
68:    }
69:
70:    private static bool NextIsScaleCall(CodeInstruction ci) =>
71:        (ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call)
72:        && ci.operand is MethodInfo mi && mi.Name == "Scale";
73:
74:    private static bool NextIsMul(CodeInstruction ci) => ci.opcode == OpCodes.Mul;
75:}

 succeeded in 651ms:
1:# Party Icon Scale
2:
3:## Overview
4:
5:Shrinks the campaign-map party-icon **leader figure and its mount** from the vanilla hardcoded `0.3` scale to
6:an MCM-configurable value (default `0.15` = half). A single Harmony transpiler rewrites both `0.3f` literals in
7:`MobilePartyVisual.AddCharacterToPartyIcon` into a call that reads the live MCM "Map Figure Scale" slider, so the
8:figures honour a runtime-tunable size instead of the engine constant.
9:
10:## Why This Exists
11:
12:On the campaign map the leader figure standing on each party icon felt oversized relative to settlements — the
13:`0.3` figure scale is large next to town/castle/village meshes. Halving it (people **and** their mount together,
14:so the rider stays proportional) makes parties read as map tokens rather than dominating the terrain. The value is
15:a slider rather than a constant because the "right" size is a visual judgement best eyeballed in-game.
16:
17:This follows the [bannerlordmodding.lt "Scale World Map Entities" guide](https://docs.bannerlordmodding.lt/guides/scale_world_map_entities/),
18:adapted to the engine's real type name and TAOM's config conventions.
19:
20:## Architecture
21:
22:**Design challenge.** The scale is a hardcoded `ldc.r4 0.3` IL literal in a private engine method — there's no
23:GameModel or virtual to override. The only seam is a transpiler. But a transpiler can't read a runtime config
24:value directly; it only edits IL. **Solution:** rewrite each `0.3` literal into `call PartyIconScaleConfig.GetScale()`
25:(a static, parameterless, `float`-returning method) — a stack-neutral swap (`ldc.r4` and the call each push one
26:float, pop none). `GetScale()` reads the MCM slider each invocation, so a slider change applies on the next icon
27:rebuild. This is the same "transpiler calls a static" pattern as `CastleAiToggle` in CastleRecruitment.
28:
29:**Two scale sites** in `AddCharacterToPartyIcon` (v1.4.6), each uniquely matchable by the instruction that
30:follows the `ldc.r4 0.3`:
31:
32:| Site | Vanilla C# | IL shape | Match rule |
33:|------|-----------|----------|-----------|
34:| Leader figure | `.Scale(0.3f)` | `ldc.r4 0.3` → `callvirt AgentVisualsData::Scale` | `0.3` immediately before a `Scale` call |
35:| Mount | `.Scale(item.ScaleFactor * 0.3f)` | `ldc.r4 0.3` → `mul` → `callvirt Scale` | `0.3` immediately before `mul` |
36:
37:The method's other `0.3` literals feed animation-speed math (`… / 0.3f` = `div`) and are not matched. If either
38:site is absent after an engine change, that swap is skipped with a warning and vanilla `0.3` is preserved — the
39:transpiler never throws (so a Harmony category re-apply can't crash).
40:
41:```
42:Patch53_PartyIconScale (thin Harmony transpiler entry)
43:        │ delegates IL surgery to
44:PartyIconScaleTranspiler.Rewrite   ← pure, synthetic-IL tested
45:        │ rewrites `ldc.r4 0.3` → `call`
46:PartyIconScaleConfig.GetScale()    ← static the rewritten IL calls
47:        │ reads + validates
48:TaomSettings.MapFigureScale        ← MCM slider
49:```
50:
51:Coexists with the BannerColorPersistence **Postfix** on the same method — a transpiler rewrites the body, a
52:postfix runs after; no conflict.
53:
54:## Configuration
55:
56:| Knob | Where | Default | Range | Notes |
57:|------|-------|---------|-------|-------|
58:| **Map Figure Scale** | MCM → TAOM → Map UI → Party Icons | `0.15` | `0.05`–`1.0` | Drives people + mounts. Vanilla = `0.30`. Applies on next icon rebuild. |
59:
60:Validation (`PartyIconScaleConfig.Resolve`): NaN / ±Infinity / out-of-range / null fall back to `Default` (0.15)
61:via `FiniteFloatValidator`. The slider UI already clamps input to `[Min, Max]`, so the guard only matters for a
62:hand-corrupted settings JSON; `Resolve` does not log on fallback because `GetScale` runs per icon rebuild and would
63:spam.
64:
65:## Key Files
66:
67:| File | Purpose |
68:|------|---------|
69:| `Main/Features/PartyIconScale/PartyIconScaleConfig.cs` | `GetScale()` (IL call target) + pure validated `Resolve()` + Default/Min/Max consts |
70:| `Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs` | IL surgery — both `0.3`→`GetScale` swaps, fail-safe |
71:| `Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.cs` | Thin transpiler entry on `MobilePartyVisual.AddCharacterToPartyIcon` |
72:| `Main/Features/TaomSettings.cs` | `MapFigureScale` MCM slider (Map UI/Party Icons group) |
73:| `Main/SubModule.cs` | `Patch53_PartyIconScale.Initialize` + `PatchCategory` registration |
74:
75:## Dependencies
76:
77:- HarmonyLib transpiler; target type `SandBox.View.Map.Visuals.MobilePartyVisual` (`SandBox.View.dll`).
78:- MCM (`TaomSettings`) for the slider; `FiniteFloatValidator` for validation.
79:
80:## Tests
81:
82:| File | Coverage |
83:|------|----------|
84:| `TAOM.Tests/Features/PartyIconScale/PartyIconScaleConfigTests.cs` | `Resolve`: valid mid/boundary pass-through; NaN/±Inf/below-min/above-max/null → Default |
85:| `TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs` | Synthetic IL: people + mount sites swap to `GetScale`; `0.325`/`0.3-before-Div` decoys untouched; labels preserved; null-getScale + missing-site fail-safe |
86:
87:The transpiler against the live engine method is verified in-game (not unit-tested — Harmony patch invocation needs a running game).
88:
89:## How-To
90:
91:**Retune the size:** move the MCM "Map Figure Scale" slider (no rebuild). `0.30` = vanilla parity; `0.05`/`1.0` =
92:bounds. Because `TaomSettings` is process-cached, the value reads live, but figures only re-render on the next icon
93:rebuild (e.g., a party moves or composition changes).
94:
95:**Also scale caravan pack animals (not currently done):** those are built in the *separate*
96:`MobilePartyVisual.AddMountToPartyIcon` method. Add a second transpiler targeting it (same `ldc.r4 0.3 → mul`
97:mount-site shape) if caravan animals should match.
98:
99:## Notes
100:
101:- Custom TAOM mounts (warg/elephant/spider) need no special handling — the mount swap is `ScaleFactor * GetScale()`,
102:  so their larger `ScaleFactor` stays proportionally large, just halved.
103:- Settlements are scaled in the editor, not via code (per the source guide) — out of scope here.

 succeeded in 640ms:
1:using System.Collections.Generic;
2:using System.Reflection;
3:using HarmonyLib;
4:using SandBox.View.Map.Visuals;
5:using TAOM.Core.Logging;
6:
7:namespace TAOM.Features.PartyIconScale.Hooks;
8:
9:/// <summary>
10:/// Patch53 — Transpiler on private <c>MobilePartyVisual.AddCharacterToPartyIcon</c>. Replaces the two
11:/// hardcoded vanilla <c>0.3f</c> campaign-map scale literals (the leader figure and its mount) with a
12:/// <c>call</c> to <see cref="PartyIconScaleConfig.GetScale"/>, so both honour the MCM "Map Figure Scale"
13:/// slider (default 0.15 = half vanilla). Thin entry point: all IL work lives in
14:/// <see cref="PartyIconScaleTranspiler"/>. Coexists with the BannerColorPersistence Postfix on the same
15:/// method (transpiler rewrites IL; postfix runs after).
16:/// </summary>
17:[HarmonyPatchCategory("Patch53_PartyIconScale")]
18:public static class Patch53_PartyIconScale
19:{
20:    private static IModLogger? _logger;
21:    private static MethodInfo? _getScale;
22:
23:    public static void Initialize(IModLogger logger)
24:    {
25:        _logger = logger;
26:        _getScale = AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));
27:    }
28:
29:    public static MethodBase? TargetMethod() =>
30:        AccessTools.Method(typeof(MobilePartyVisual), "AddCharacterToPartyIcon");
31:
32:    [HarmonyTranspiler]
33:    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
34:        PartyIconScaleTranspiler.Rewrite(instructions, _getScale, _logger);
35:}

 succeeded in 656ms:

  Main\Features\TaomSettings.cs:469:        HintText = "Show diagnostic [BattleActionBar] messages.")]
  Main\Features\TaomSettings.cs:470:    public bool BattleActionBarDebug { get; set; } = false;
  Main\Features\TaomSettings.cs:471:
> Main\Features\TaomSettings.cs:472:    // --- [7mMap UI[0m / Settlement Nameplates ---[0m
[7m[0m  Main\Features\TaomSettings.cs:473:[0m
[7m[0m  Main\Features\TaomSettings.cs:474:    // --- World / Bandit Scaling ---[0m
[7m[0m  Main\Features\TaomSettings.cs:475:[0m
[7m[0m  Main\Features\TaomSettings.cs:476:    [SettingPropertyGroup("World/Bandit Scaling", GroupOrder = 35)][0m
[7m[0m  Main\Features\TaomSettings.cs:477:    [SettingPropertyBool("Enable Bandit Scaling", Order = 0,[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:478: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mHintText [0m[7m[0m= [0m[7m[0m"Master [0m[7m[0mtoggle. [0m[7m[0mWhen [0m[7m[0moff, [0m[7m[0mhideout [0m[7m[0mdensity [0m[7m[0m+ [0m[7m[0mbandit [0m[7m[0mparty [0m[7m[0msizes [0m
[7m[0muse [0m[7m[0mvanilla [0m[7m[0mvalues. [0m[7m[0mWhen [0m[7m[0mon, [0m[7m[0mboth [0m[7m[0mscale [0m[7m[0mwith [0m[7m[0mPlayerProgress [0m[7m[0m(0.0 [0m[7m[0mnew [0m[7m[0mcampaign [0m[7m[0m-> [0m[7m[0m1.0 [0m[7m[0mendgame) [0m[7m[0mper [0m[7m[0mthe [0m[7m[0mcurves [0m[7m[0mbelow.")][0m
  Main\Features\TaomSettings.cs:547:        HintText = "When ON (default), AI lords' parties can also sail. When OFF, 
only the player sails and AI stays on land — the conservative option if AI naval routing looks odd.")]
  Main\Features\TaomSettings.cs:548:    public bool NavalTravelApplyToAi { get; set; } = true;
  Main\Features\TaomSettings.cs:549:
> Main\Features\TaomSettings.cs:550:    // --- [7mMap UI[0m / Settlement Nameplates ---[0m
[7m[0m  Main\Features\TaomSettings.cs:551:[0m
> Main\Features\TaomSettings.cs:552:    [SettingPropertyGroup("[7mMap UI[0m/Settlement Nameplates", GroupOrder = 40)][0m
[7m[0m  Main\Features\TaomSettings.cs:553:    [SettingPropertyBool("Enable Settlement Nameplate Fade", Order = 0,[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:554: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mHintText [0m[7m[0m= [0m[7m[0m"Fade [0m[7m[0msettlement [0m[7m[0mnameplates [0m[7m[0mwith [0m[7m[0mcamera [0m[7m[0mdistance. [0m[7m[0mWhen [0m[7m[0moff, [0m[7m[0mall [0m
[7m[0mnameplates [0m[7m[0mdisplay [0m[7m[0mat [0m[7m[0mfull [0m[7m[0mvisibility [0m[7m[0mregardless [0m[7m[0mof [0m[7m[0mdistance [0m[7m[0m(vanilla).")][0m
[7m[0m  Main\Features\TaomSettings.cs:555:    public bool EnableNameplateFade { get; set; } = true;[0m
[7m[0m  Main\Features\TaomSettings.cs:556:[0m
> Main\Features\TaomSettings.cs:557:    [SettingPropertyGroup("[7mMap UI[0m/Settlement Nameplates")][0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:558: [0m[7m [0m[7m [0m[7m [0m[7m[0m[SettingPropertyFloatingInteger("Fade [0m[7m[0mStart [0m[7m[0mDistance", [0m[7m[0m5f, [0m[7m[0m500f, [0m[7m[0m"#0", [0m[7m[0mOrder [0m[7m[0m= [0m
[7m[0m1,[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:559: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mHintText [0m[7m[0m= [0m[7m[0m"Camera [0m[7m[0mdistance [0m[7m[0mat [0m[7m[0mwhich [0m[7m[0mfade [0m[7m[0mbegins. [0m[7m[0mNameplates [0m[7m[0mcloser [0m[7m[0mthan [0m
[7m[0mthis [0m[7m[0mstay [0m[7m[0mfully [0m[7m[0mopaque. [0m[7m[0mDefault [0m[7m[0m80.")][0m
[7m[0m  Main\Features\TaomSettings.cs:560:    public float NameplateFadeNearDistance { get; set; } = 80f;[0m
[7m[0m  Main\Features\TaomSettings.cs:561:[0m
> Main\Features\TaomSettings.cs:562:    [SettingPropertyGroup("[7mMap UI[0m/Settlement Nameplates")][0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:563: [0m[7m [0m[7m [0m[7m [0m[7m[0m[SettingPropertyFloatingInteger("Fade [0m[7m[0mEnd [0m[7m[0mDistance", [0m[7m[0m10f, [0m[7m[0m1000f, [0m[7m[0m"#0", [0m[7m[0mOrder [0m[7m[0m= [0m
[7m[0m2,[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:564: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mHintText [0m[7m[0m= [0m[7m[0m"Camera [0m[7m[0mdistance [0m[7m[0mat [0m[7m[0mwhich [0m[7m[0mfade [0m[7m[0mcompletes. [0m[7m[0mNameplates [0m[7m[0mfarther [0m
[7m[0mthan [0m[7m[0mthis [0m[7m[0mare [0m[7m[0mfully [0m[7m[0mhidden. [0m[7m[0mMust [0m[7m[0mbe [0m[7m[0mgreater [0m[7m[0mthan [0m[7m[0mFade [0m[7m[0mStart [0m[7m[0mDistance. [0m[7m[0mDefault [0m[7m[0m200.")][0m
[7m[0m  Main\Features\TaomSettings.cs:565:    public float NameplateFadeFarDistance { get; set; } = 200f;[0m
[7m[0m  Main\Features\TaomSettings.cs:566:[0m
> Main\Features\TaomSettings.cs:567:    // --- [7mMap UI[0m / Party Icons ---[0m
[7m[0m  Main\Features\TaomSettings.cs:568:[0m
> Main\Features\TaomSettings.cs:569:    [SettingPropertyGroup("[7mMap UI[0m/Party Icons", GroupOrder = 41)][0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:570: [0m[7m [0m[7m [0m[7m [0m[7m[0m[SettingPropertyFloatingInteger("Map [0m[7m[0mFigure [0m[7m[0mScale", [0m[7m[0m0.05f, [0m[7m[0m1.0f, [0m[7m[0m"#0.00", [0m
[7m[0mOrder [0m[7m[0m= [0m[7m[0m0,[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:571: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mHintText [0m[7m[0m= [0m[7m[0m"Size [0m[7m[0mof [0m[7m[0mparty-icon [0m[7m[0mfigures [0m[7m[0mand [0m[7m[0mtheir [0m[7m[0mmounts [0m[7m[0mon [0m[7m[0mthe [0m[7m[0mcampaign [0m
[7m[0mmap. [0m[7m[0mVanilla [0m[7m[0m= [0m[7m[0m0.30; [0m[7m[0mdefault [0m[7m[0m0.15 [0m[7m[0m= [0m[7m[0mhalf [0m[7m[0m(makes [0m[7m[0mparties [0m[7m[0mfeel [0m[7m[0msmaller [0m[7m[0mrelative [0m[7m[0mto [0m[7m[0msettlements). [0m[7m[0mApplies [0m[7m[0mon [0m[7m[0mthe [0m[7m[0mnext [0m
[7m[0micon [0m[7m[0mrebuild [0m[7m[0mafter [0m[7m[0mchanging.")][0m
> Main\Features\TaomSettings.cs:572:    public float [7mMapFigureScale[0m { get; set; } = 0.15f;[0m
[7m[0m  Main\Features\TaomSettings.cs:573:[0m
[7m[0m  Main\Features\TaomSettings.cs:574:    // --- Map Tools / Distance Cache Rebuild ---[0m
[7m[0m  Main\Features\TaomSettings.cs:575:    //[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:576: [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mRebuilds [0m
[7m[0mModules/TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:577: [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mfrom [0m[7m[0mthe [0m[7m[0mlive [0m[7m[0mcampaign's [0m[7m[0mmap [0m[7m[0mscene. [0m[7m[0mThe [0m[7m[0mvanilla [0m[7m[0meditor's [0m[7m[0mComputeAndSave [0m
[7m[0mbutton [0m[7m[0mdoes [0m[7m[0mthe[0m
[7m [0m[7m [0m[7m[0mMain\Features\TaomSettings.cs:578: [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0msame [0m[7m[0mthing [0m[7m[0mbut [0m[7m[0mtakes [0m[7m[0m~108 [0m[7m[0mhours [0m[7m[0mon [0m[7m[0mTAOM's [0m[7m[0m863-settlement [0m[7m[0mmap. [0m[7m[0mOur [0m[7m[0mparallel [0m
[7m[0m+ [0m[7m[0msmoke-test [0m[7m[0m+[0m


 succeeded in 645ms:

  Main\SubModule.cs:634:        
SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
  Main\SubModule.cs:635:        _harmony.PatchCategory("Patch38_SettlementNameplateFade");
  Main\SubModule.cs:636:
> Main\SubModule.cs:637:        // [7mPatch53[0m_PartyIconScale [0m[7m[0m— [0m[7m[0mtranspiler [0m[7m[0mthat [0m[7m[0mrewrites [0m[7m[0mthe [0m[7m[0mtwo [0m[7m[0mhardcoded [0m[7m[0m0.3f [0m
[7m[0mcampaign-map [0m[7m[0mscale[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:638: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mliterals [0m[7m[0min [0m[7m[0mMobilePartyVisual.AddCharacterToPartyIcon [0m[7m[0m(leader [0m[7m[0mfigure [0m[7m[0m+ [0m[7m[0mits [0m[7m[0mmount) [0m
[7m[0minto [0m[7m[0ma [0m[7m[0mcall[0m
> Main\SubModule.cs:639:        // to [7mPartyIconScale[0mConfig.GetScale(), so both honour the MCM "Map Figure Scale" slider[0m
[7m[0m  Main\SubModule.cs:640:        // (default 0.15 = half vanilla). See docs/features/party-icon-scale.md.[0m
> Main\SubModule.cs:641:        
Features.[7mPartyIconScale[0m.Hooks.Patch53_PartyIconScale.Initialize(IoC.Resolve<IModLogger>());[0m
> Main\SubModule.cs:642:        _harmony.PatchCategory("[7mPatch53[0m_PartyIconScale");[0m
[7m[0m  Main\SubModule.cs:643:[0m
[7m[0m  Main\SubModule.cs:644:        // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an[0m
[7m[0m  Main\SubModule.cs:645:        // intermittent battle-load hang leaves a log whose last line names the stuck phase[0m


codex
The core implementation matches the described shape so far: thin Harmony entry, pure transpiler helper, static call target, and MCM bounds at `0.05..1.0`. I’m now checking test power, sibling precedent, and the coexisting postfix so the verdict is not just a surface read.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'TAOM.Tests/Features/PartyIconScale/PartyIconScaleConfigTests.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/Features/CastleRecruitment/Hooks/CastleAiToggle.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/Features/BannerColorPersistence/Hooks/MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/Core/Validation/FiniteFloatValidator.cs' | ForEach-Object { "'$n += 1; '"'{0}:{1}' -f "'$n, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 585ms:
1:using TaleWorlds.CampaignSystem.Settlements;
2:
3:namespace TAOM.Features.CastleRecruitment.Hooks;
4:
5:/// <summary>
6:/// Runtime toggle consulted by the AI-scoring transpilers. The transpilers replace the single
7:/// <c>settlement.IsCastle</c> call inside <c>AiVisitSettlementBehavior</c>'s
8:/// <c>!settlement.IsCastle &amp;&amp; ...</c> recruitment gates with a call to
9:/// <see cref="IsCastleAndAiDisabled"/> — identical stack shape (Settlement → bool), but the result
10:/// is forced to <c>false</c> when AI castle recruitment is enabled, so the <c>!IsCastle</c> gate
11:/// passes for castles. When disabled it returns the real <c>IsCastle</c>, preserving vanilla scoring.
12:/// This keeps the feature MCM-toggleable at runtime without re-patching.
13:/// </summary>
14:public static class CastleAiToggle
15:{
16:    private static ICastleRecruitmentSettingsProvider? _settings;
17:
18:    public static void Initialize(ICastleRecruitmentSettingsProvider settings) => _settings = settings;
19:
20:    public static bool IsCastleAndAiDisabled(Settlement settlement)
21:    {
22:        bool aiOn = (_settings?.IsEnabled ?? false) && (_settings?.IsAiEnabled ?? false);
23:        return !aiOn && settlement.IsCastle;
24:    }
25:}

 succeeded in 593ms:
1:using Microsoft.VisualStudio.TestTools.UnitTesting;
2:using TAOM.Features.PartyIconScale;
3:
4:namespace TAOM.Tests.Features.PartyIconScale;
5:
6:[TestClass]
7:public class PartyIconScaleConfigTests
8:{
9:    private const float Delta = 1e-6f;
10:
11:    [TestMethod]
12:    public void Resolve_ValidMidRange_ReturnsValue()
13:    {
14:        Assert.AreEqual(0.2f, PartyIconScaleConfig.Resolve(0.2f), Delta);
15:    }
16:
17:    [TestMethod]
18:    public void Resolve_DefaultSliderValue_ReturnsItself()
19:    {
20:        Assert.AreEqual(PartyIconScaleConfig.Default,
21:            PartyIconScaleConfig.Resolve(PartyIconScaleConfig.Default), Delta);
22:    }
23:
24:    [TestMethod]
25:    public void Resolve_AtMinBoundary_ReturnsValue()
26:    {
27:        Assert.AreEqual(PartyIconScaleConfig.Min,
28:            PartyIconScaleConfig.Resolve(PartyIconScaleConfig.Min), Delta);
29:    }
30:
31:    [TestMethod]
32:    public void Resolve_AtMaxBoundary_ReturnsValue()
33:    {
34:        Assert.AreEqual(PartyIconScaleConfig.Max,
35:            PartyIconScaleConfig.Resolve(PartyIconScaleConfig.Max), Delta);
36:    }
37:
38:    [TestMethod]
39:    public void Resolve_Null_ReturnsDefault()
40:    {
41:        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(null), Delta);
42:    }
43:
44:    [TestMethod]
45:    public void Resolve_NaN_ReturnsDefault()
46:    {
47:        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(float.NaN), Delta);
48:    }
49:
50:    [TestMethod]
51:    public void Resolve_PositiveInfinity_ReturnsDefault()
52:    {
53:        Assert.AreEqual(PartyIconScaleConfig.Default,
54:            PartyIconScaleConfig.Resolve(float.PositiveInfinity), Delta);
55:    }
56:
57:    [TestMethod]
58:    public void Resolve_NegativeInfinity_ReturnsDefault()
59:    {
60:        Assert.AreEqual(PartyIconScaleConfig.Default,
61:            PartyIconScaleConfig.Resolve(float.NegativeInfinity), Delta);
62:    }
63:
64:    [TestMethod]
65:    public void Resolve_BelowMin_ReturnsDefault()
66:    {
67:        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(0.01f), Delta);
68:    }
69:
70:    [TestMethod]
71:    public void Resolve_AboveMax_ReturnsDefault()
72:    {
73:        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(2.0f), Delta);
74:    }
75:}

 succeeded in 528ms:
1:using System;
2:using System.Collections.Generic;
3:using System.Reflection;
4:using System.Reflection.Emit;
5:using HarmonyLib;
6:using TaleWorlds.CampaignSystem.Settlements;
7:using TAOM.Core.Logging;
8:
9:namespace TAOM.Features.CastleRecruitment.Hooks;
10:
11:/// <summary>
12:/// Shared IL surgery for the two AI-scoring transpilers. Swaps the recruitment-gate <c>get_IsCastle</c>
13:/// for a static call to <see cref="CastleAiToggle"/>.
14:///
15:/// Targets the FIRST <c>get_IsCastle</c> in the method — in both target methods the recruitment gate is
16:/// the first (AiHourlyTick: line ~269 recruit gate vs. the later line ~317 reform-score gate;
17:/// FillSettlements: a single get_IsCastle) — and additionally requires a uniquely-named anchor method
18:/// to appear shortly after it. This double check means a future engine refactor that reorders or
19:/// renames the gate makes the patch FAIL-SAFE (log + return the original stream, AI keeps vanilla
20:/// behaviour) rather than swap the wrong IsCastle. We never search past the first get_IsCastle, so the
21:/// later reform-score gate can never be hit by accident.
22:/// </summary>
23:internal static class CastleAiTranspiler
24:{
25:    private const int AnchorWindow = 24;
26:
27:    internal static List<CodeInstruction> SwapIsCastleGate(
28:        IEnumerable<CodeInstruction> instructions, string anchorMethodName, string label, IModLogger? logger)
29:    {
30:        var list = new List<CodeInstruction>(instructions);
31:        var getIsCastle = AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.IsCastle));
32:        var replacement = AccessTools.Method(typeof(CastleAiToggle), nameof(CastleAiToggle.IsCastleAndAiDisabled));
33:
34:        if (getIsCastle == null || replacement == null)
35:        {
36:            logger?.LogWarning($"[CastleRecruitment] {label}: reflection lookup failed — AI castle gate not patched (vanilla behaviour preserved)");
37:            return list;
38:        }
39:
40:        int firstIsCastle = -1;
41:        for (int i = 0; i < list.Count; i++)
42:        {
43:            if (IsGetIsCastle(list[i], getIsCastle))
44:            {
45:                firstIsCastle = i;
46:                break;
47:            }
48:        }
49:
50:        if (firstIsCastle < 0)
51:        {
52:            logger?.LogWarning($"[CastleRecruitment] {label}: no get_IsCastle found — AI castle gate not patched (vanilla behaviour preserved)");
53:            return list;
54:        }
55:
56:        // Confirm the FIRST get_IsCastle is the recruitment gate by requiring the expected anchor
57:        // method shortly after it. If absent, bail — never fall through to a different get_IsCastle.
58:        bool anchored = false;
59:        int end = Math.Min(list.Count, firstIsCastle + 1 + AnchorWindow);
60:        for (int j = firstIsCastle + 1; j < end; j++)
61:        {
62:            if (IsCallNamed(list[j], anchorMethodName))
63:            {
64:                anchored = true;
65:                break;
66:            }
67:        }
68:
69:        if (!anchored)
70:        {
71:            logger?.LogWarning($"[CastleRecruitment] {label}: first get_IsCastle not followed by anchor '{anchorMethodName}' — AI castle gate not patched (vanilla behaviour preserved)");
72:            return list;
73:        }
74:
75:        // Same stack shape: callvirt instance get_IsCastle(Settlement)->bool  becomes
76:        // call static IsCastleAndAiDisabled(Settlement)->bool. Labels remain on the same object.
77:        list[firstIsCastle].opcode = OpCodes.Call;
78:        list[firstIsCastle].operand = replacement;
79:        logger?.LogDebug($"[CastleRecruitment] {label}: IsCastle gate swapped at instruction {firstIsCastle}");
80:        return list;
81:    }
82:
83:    private static bool IsGetIsCastle(CodeInstruction ci, MethodInfo getIsCastle) =>
84:        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
85:        && ci.operand is MethodInfo mi
86:        && mi.Name == getIsCastle.Name
87:        && mi.DeclaringType == getIsCastle.DeclaringType;
88:
89:    private static bool IsCallNamed(CodeInstruction ci, string name) =>
90:        (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
91:        && ci.operand is MethodInfo mi
92:        && mi.Name == name;
93:}

 succeeded in 598ms:
1:using System.Collections.Generic;
2:using System.Reflection;
3:using System.Reflection.Emit;
4:using HarmonyLib;
5:using Microsoft.VisualStudio.TestTools.UnitTesting;
6:using NSubstitute;
7:using TAOM.Core.Logging;
8:using TAOM.Features.PartyIconScale;
9:
10:namespace TAOM.Tests.Features.PartyIconScale;
11:
12:[TestClass]
13:public class PartyIconScaleTranspilerTests
14:{
15:    private static readonly MethodInfo GetScale =
16:        AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));
17:
18:    // Stand-in for AgentVisualsData.Scale as the Callvirt operand. The transpiler matches on the
19:    // method NAME only ("Scale"), so any MethodInfo named "Scale" reproduces the IL shape.
20:    private static float Scale(float x) => x;
21:
22:    private static readonly MethodInfo ScaleStub =
23:        typeof(PartyIconScaleTranspilerTests).GetMethod(nameof(Scale),
24:            BindingFlags.NonPublic | BindingFlags.Static)!;
25:
26:    private IModLogger _logger = null!;
27:
28:    [TestInitialize]
29:    public void Setup() => _logger = Substitute.For<IModLogger>();
30:
31:    [TestMethod]
32:    public void Rewrite_PeopleSite_SwapsLdcForGetScaleCall()
33:    {
34:        var input = new List<CodeInstruction>
35:        {
36:            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
37:            new CodeInstruction(OpCodes.Callvirt, ScaleStub),
38:        };
39:
40:        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);
41:
42:        Assert.AreEqual(OpCodes.Call, result[0].opcode);
43:        Assert.AreSame(GetScale, result[0].operand);
44:        Assert.AreEqual(OpCodes.Callvirt, result[1].opcode); // Scale call left intact
45:    }
46:
47:    [TestMethod]
48:    public void Rewrite_MountSite_SwapsLdcForGetScaleCall()
49:    {
50:        var input = new List<CodeInstruction>
51:        {
52:            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
53:            new CodeInstruction(OpCodes.Mul),
54:        };
55:
56:        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);
57:
58:        Assert.AreEqual(OpCodes.Call, result[0].opcode);
59:        Assert.AreSame(GetScale, result[0].operand);
60:        Assert.AreEqual(OpCodes.Mul, result[1].opcode); // multiply (ScaleFactor * scale) left intact
61:    }
62:
63:    [TestMethod]
64:    public void Rewrite_BothSitesWithDecoys_SwapsExactlyTheTwoZeroPointThree()
65:    {
66:        var input = new List<CodeInstruction>
67:        {
68:            new CodeInstruction(OpCodes.Ldc_R4, 0.325f),      // [0] decoy: wrong literal, before Mul
69:            new CodeInstruction(OpCodes.Mul),                 // [1]
70:            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // [2] PEOPLE: 0.3 before Callvirt Scale
71:            new CodeInstruction(OpCodes.Callvirt, ScaleStub), // [3]
72:            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // [4] decoy: 0.3 before Div (anim math)
73:            new CodeInstruction(OpCodes.Div),                 // [5]
74:            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),        // [6] MOUNT: 0.3 before Mul
75:            new CodeInstruction(OpCodes.Mul),                 // [7]
76:        };
77:
78:        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);
79:
80:        Assert.AreEqual(OpCodes.Call, result[2].opcode, "people 0.3 should swap to GetScale call");
81:        Assert.AreEqual(OpCodes.Call, result[6].opcode, "mount 0.3 should swap to GetScale call");
82:        Assert.AreEqual(OpCodes.Ldc_R4, result[0].opcode, "0.325 decoy untouched");
83:        Assert.AreEqual(OpCodes.Ldc_R4, result[4].opcode, "0.3-before-Div decoy untouched");
84:    }
85:
86:    [TestMethod]
87:    public void Rewrite_PreservesLabelsOnSwappedInstruction()
88:    {
89:        var label = new Label();
90:        var ldc = new CodeInstruction(OpCodes.Ldc_R4, 0.3f);
91:        ldc.labels.Add(label);
92:        var input = new List<CodeInstruction>
93:        {
94:            ldc,
95:            new CodeInstruction(OpCodes.Mul),
96:        };
97:
98:        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);
99:
100:        Assert.AreEqual(OpCodes.Call, result[0].opcode);
101:        CollectionAssert.Contains(result[0].labels, label);
102:    }
103:
104:    [TestMethod]
105:    public void Rewrite_NullGetScale_ReturnsUnchangedAndWarns()
106:    {
107:        var input = new List<CodeInstruction>
108:        {
109:            new CodeInstruction(OpCodes.Ldc_R4, 0.3f),
110:            new CodeInstruction(OpCodes.Mul),
111:        };
112:
113:        var result = PartyIconScaleTranspiler.Rewrite(input, null, _logger);
114:
115:        Assert.AreEqual(OpCodes.Ldc_R4, result[0].opcode); // untouched — vanilla 0.3 preserved
116:        _logger.Received().LogWarning(Arg.Any<string>());
117:    }
118:
119:    [TestMethod]
120:    public void Rewrite_PeopleSiteMissing_WarnsButStillSwapsMount()
121:    {
122:        var input = new List<CodeInstruction>
123:        {
124:            new CodeInstruction(OpCodes.Ldc_R4, 0.3f), // mount-only stream
125:            new CodeInstruction(OpCodes.Mul),
126:        };
127:
128:        var result = PartyIconScaleTranspiler.Rewrite(input, GetScale, _logger);
129:
130:        Assert.AreEqual(OpCodes.Call, result[0].opcode); // mount still swapped
131:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("people")));
132:    }
133:}

 succeeded in 574ms:
1:using System.Reflection;
2:using HarmonyLib;
3:using SandBox.View.Map.Visuals;
4:using TAOM.Adapters;
5:using TaleWorlds.CampaignSystem;
6:using TaleWorlds.CampaignSystem.Party;
7:using TaleWorlds.Core;
8:using TaleWorlds.MountAndBlade;
9:
10:namespace TAOM.Features.BannerColorPersistence.Hooks;
11:
12:public static class MobilePartyVisual_AddCharacterToPartyIcon_Patch
13:{
14:    private static IBannerColorService? _service;
15:    private static IBannerHeroAdapter? _heroAdapter;
16:
17:    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
18:    {
19:        _service = service;
20:        _heroAdapter = heroAdapter;
21:    }
22:
23:    public static MethodBase? TargetMethod()
24:    {
25:        // Phase 9b #159 — drop the explicit param-type array. The method has exactly one overload
26:        // in v1.3.15 (verified via ilspycmd on SandBox.View.dll), so name-only resolution is
27:        // unambiguous. The previous array included `typeof(ActionIndexCache).MakeByRefType()` for
28:        // the two `in ActionIndexCache` params — `in` is modreq-qualified in IL and Harmony 2's
29:        // AccessTools is inconsistent about matching modreq. If resolution failed, the LogWarning
30:        // would fire and party-icon colors silently stop persisting. Name-only resolution is
31:        // robust because there's no ambiguity to resolve.
32:        return AccessTools.Method(typeof(MobilePartyVisual), "AddCharacterToPartyIcon");
33:    }
34:
35:    public static void Postfix(CharacterObject characterObject, ref uint teamColor1, ref uint teamColor2)
36:    {
37:        var info = _heroAdapter?.GetClanColorInfo(characterObject);
38:        if (info == null) return;
39:        if (!(_service?.ShouldUseClanColor(info.Value) ?? false)) return;
40:
41:        teamColor1 = info.Value.Color1;
42:        teamColor2 = info.Value.Color2;
43:    }
44:}

 succeeded in 592ms:
1:namespace TAOM.Core.Validation;
2:
3:/// <summary>
4:/// Centralized float-validation helpers for config providers.
5:///
6:/// Why this exists: range checks like `value &lt; min || value &gt; max` evaluate false for `NaN`
7:/// (all NaN comparisons return false per IEEE-754), so a `NaN` config value sneaks past validation
8:/// and then breaks downstream comparisons in unpredictable ways. This has shipped twice:
9:///
10:/// * Career cooldown review #31 (2026-05-04) — NaN cooldown made `IsOnCooldown =&gt; CooldownRemaining &gt; 0f`
11:///   evaluate false → ability "always ready" → V re-activates indefinitely.
12:/// * EditorCacheRebuild Codex review #38 (2026-05-12) — NaN `SmokeTestDistanceTolerance` made the gate's
13:///   `maxDelta &gt; tolerance` evaluate false → smoke test silently disabled → potential threading
14:///   issues never caught.
15:///
16:/// Use these helpers BEFORE every range check on a `float`/`double` config field. Bool/int fields
17:/// don't need this — only IEEE-754 types are affected.
18:/// </summary>
19:public static class FiniteFloatValidator
20:{
21:    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
22:    public static bool IsFinite(float value) =>
23:        !float.IsNaN(value) && !float.IsInfinity(value);
24:
25:    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
26:    public static bool IsFinite(double value) =>
27:        !double.IsNaN(value) && !double.IsInfinity(value);
28:
29:    /// <summary>
30:    /// Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).
31:    /// NaN/Infinity always return false. Standard pattern for config range validation.
32:    /// </summary>
33:    public static bool IsFiniteInRange(float value, float min, float max) =>
34:        IsFinite(value) && value >= min && value <= max;
35:
36:    /// <summary>
37:    /// Returns true if <paramref name="value"/> is finite AND less than or equal to <paramref name="max"/>.
38:    /// Use for penalty fields constrained to be non-positive (e.g., loyalty penalties must be ≤ 0).
39:    /// NaN/Infinity always return false.
40:    /// </summary>
41:    public static bool IsFiniteAtMost(float value, float max) =>
42:        IsFinite(value) && value <= max;
43:
44:    /// <summary>
45:    /// Returns true if <paramref name="value"/> is finite AND greater than or equal to <paramref name="min"/>.
46:    /// Use for bonus fields constrained to be non-negative.
47:    /// NaN/Infinity always return false.
48:    /// </summary>
49:    public static bool IsFiniteAtLeast(float value, float min) =>
50:        IsFinite(value) && value >= min;
51:
52:    // Phase 9b #126 — double overloads. InitialChildGeneration (and other features that parse
53:    // JSON via Newtonsoft `Value<double?>()`) need double-typed validation. Same semantics as
54:    // the float overloads — NaN/Infinity always fail, range is inclusive.
55:
56:    /// <summary>Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).</summary>
57:    public static bool IsFiniteInRange(double value, double min, double max) =>
58:        IsFinite(value) && value >= min && value <= max;
59:
60:    /// <summary>Returns true if <paramref name="value"/> is finite AND ≤ <paramref name="max"/>.</summary>
61:    public static bool IsFiniteAtMost(double value, double max) =>
62:        IsFinite(value) && value <= max;
63:
64:    /// <summary>Returns true if <paramref name="value"/> is finite AND ≥ <paramref name="min"/>.</summary>
65:    public static bool IsFiniteAtLeast(double value, double min) =>
66:        IsFinite(value) && value >= min;
67:}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/SubModule.cs' -Pattern 'OnSubModuleLoad|OnGameInitializationFinished|PatchCategory' -Context 1,2" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/SubModule.cs' -Pattern 'BannerColor|MobilePartyVisual_AddCharacterToPartyIcon' -Context 3,4" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/PartyIconScale' -Recurse | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'TAOM.Tests/Features/PartyIconScale' -Recurse | Select-Object FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/**/*.cs','TAOM.Tests/**/*.cs' -Pattern 'PartyIconScale|MapFigureScale' -List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 596ms:

[32;1mFullName                                                                                            [0m[32;1m Length[0m
[32;1m--------                                                                                            [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs       2169
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs   5037


 succeeded in 604ms:

[32;1mFullName                                                                                     [0m[32;1m Length[0m
[32;1m--------                                                                                     [0m [32;1m------[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks                           
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs         2653
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs     3316
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs 1483


 succeeded in 630ms:

  Main\SubModule.cs:94:
> Main\SubModule.cs:95:    protected override void [7mOnSubModuleLoad[0m()[0m
[7m[0m  Main\SubModule.cs:96:    {[0m
> Main\SubModule.cs:97:        base.[7mOnSubModuleLoad[0m();[0m
[7m[0m  Main\SubModule.cs:98:[0m
[7m[0m  Main\SubModule.cs:99:        IoC.Configure();[0m
  Main\SubModule.cs:101:        // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
> Main\SubModule.cs:102:        // after IoC.Configure() so its Finalizers cover the rest of [7mOnSubModuleLoad[0m
> Main\SubModule.cs:103:        // (UIExtender init, time-acceleration resolve, downstream [7mPatchCategory[0m calls).[0m
[7m[0m  Main\SubModule.cs:104:        // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot[0m
[7m[0m  Main\SubModule.cs:105:        // is the IoC.Configure() call itself — if THAT throws, the entire feature is[0m
  Main\SubModule.cs:112:            {
> Main\SubModule.cs:113:                _harmony.[7mPatchCategory[0m("Patch37_CrashReport");[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:114: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m
[7m[0mIoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:115: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mif [0m
[7m[0m((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) [0m[7m[0m?? [0m[7m[0mtrue)[0m
  Main\SubModule.cs:133:        // UIExtenderEx's [PrefabExtension] hook — so this is a Harmony Postfix, not a 
PrefabExtension.
> Main\SubModule.cs:134:        // MUST be applied here in [7mOnSubModuleLoad[0m: MCM's ResourceInjector.Inject() runs at[0m
> Main\SubModule.cs:135:        // OnBeforeInitialModuleScreenSetAsRoot (after every module's [7mOnSubModuleLoad[0m), [0m[7m[0mso [0m[7m[0mthe [0m
[7m[0mPostfix[0m
[7m[0m  Main\SubModule.cs:136:        // must already be attached when MCM calls CreateAndRegister.[0m
> Main\SubModule.cs:137:        _harmony.[7mPatchCategory[0m("Patch41_McmLayoutFix");[0m
[7m[0m  Main\SubModule.cs:138:[0m
[7m[0m  Main\SubModule.cs:139:        _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();[0m
  Main\SubModule.cs:142:        // Loads English string overrides from taom_module_strings.xml (removes hardcoded 
"The" articles).
> Main\SubModule.cs:143:        _harmony.[7mPatchCategory[0m("Patch25_LocalizationOverride");[0m
[7m[0m  Main\SubModule.cs:144:        var pathService0 = IoC.Resolve<IPathService>();[0m
[7m[0m  Main\SubModule.cs:145:        var logger0 = IoC.Resolve<IModLogger>();[0m
  Main\SubModule.cs:158:
> Main\SubModule.cs:159:        _harmony.[7mPatchCategory[0m("Patch18_CulturalFeats");[0m
> Main\SubModule.cs:160:        _harmony.[7mPatchCategory[0m("Patch19_CustomBattles");[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:161: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mPatch0_BattleScenes: [0m[7m[0mloads [0m[7m[0mTAOM's [0m[7m[0msp_battle_scenes.xml [0m[7m[0m(full [0m[7m[0m0-255 [0m[7m[0mmap_indices [0m
[7m[0mcoverage) [0m[7m[0mso [0m[7m[0mthe[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:162: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mTAOM_Map [0m[7m[0mMain_map [0m[7m[0mgrid's [0m[7m[0mextended [0m[7m[0mindices [0m[7m[0m(158-255) [0m[7m[0mresolve [0m[7m[0mto [0m[7m[0mreal [0m[7m[0mbattle [0m[7m[0mterrains [0m
[7m[0minstead [0m[7m[0mof[0m
  Main\SubModule.cs:165:        // pending the worldmap_battle_scene_grid re-author. See 
docs/reference/worldmap-battle-scene-grid.md.
> Main\SubModule.cs:166:        _harmony.[7mPatchCategory[0m("Patch0_BattleScenes");[0m
> Main\SubModule.cs:167:        // Remaining patches applied in [7mOnGameInitializationFinished [0m[7m[0m— [0m[7m[0mView [0m[7m[0massembly [0m[7m[0mmust [0m[7m[0mbe [0m
[7m[0minitialized [0m[7m[0mfirst[0m
[7m[0m  Main\SubModule.cs:168:[0m
[7m[0m  Main\SubModule.cs:169:        var pathService = IoC.Resolve<IPathService>();[0m
  Main\SubModule.cs:199:
> Main\SubModule.cs:200:        _harmony.[7mPatchCategory[0m("Patch21_ShaderPrecompilation");[0m
[7m[0m  Main\SubModule.cs:201:        _shaderRunner = IoC.Resolve<ShaderPrecompileRunner>();[0m
[7m[0m  Main\SubModule.cs:202:        ShaderPrecompilationIoC.InitializeHooks(logger, _shaderRunner);[0m
  Main\SubModule.cs:203:
> Main\SubModule.cs:204:        _harmony.[7mPatchCategory[0m("Patch22_ArmyTargeting");[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:205: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mPatch49: [0m[7m[0mFinalizer [0m[7m[0mguarding [0m[7m[0mvanilla [0m
[7m[0mArmy.FindBestGatheringSettlementAndMoveTheLeader,[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:206: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mwhich [0m[7m[0mNREs [0m[7m[0m(Army.cs:726 [0m[7m[0msettlement.GatePosition [0m[7m[0m/ [0m[7m[0m659 [0m[7m[0mKingdom.Settlements, [0m[7m[0mv1.4.6) [0m
[7m[0mwhen [0m[7m[0ma[0m
  Main\SubModule.cs:209:        // Crash report 2026-06-17. See the patch's doc-comment.
> Main\SubModule.cs:210:        _harmony.[7mPatchCategory[0m("Patch49_ArmyGatheringNreGuard");[0m
> Main\SubModule.cs:211:        _harmony.[7mPatchCategory[0m("Patch30_MixedFormations");[0m
[7m[0m  Main\SubModule.cs:212:        // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +[0m
[7m[0m  Main\SubModule.cs:213:        // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in[0m
  Main\SubModule.cs:214:        // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
> Main\SubModule.cs:215:        // which is null during [7mOnSubModuleLoad[0m and would crash JIT prep with NRE.[0m
[7m[0m  Main\SubModule.cs:216:[0m
[7m[0m  Main\SubModule.cs:217:        var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();[0m
  Main\SubModule.cs:247:        // + AiVisitSettlementBehavior (both in TaleWorlds.CampaignSystem, no 
View/Mission.cctor
> Main\SubModule.cs:248:        // dependency, safe in [7mOnSubModuleLoad[0m). [0m[7m[0mThe [0m[7m[0mtranspilers [0m[7m[0mswap [0m[7m[0mthe [0m[7m[0mAI [0m[7m[0mIsCastle [0m[7m[0mgate [0m[7m[0mto [0m
[7m[0ma [0m[7m[0mruntime[0m
[7m[0m  Main\SubModule.cs:249:        // toggle; the postfix invokes the private CheckRecruiting for castles. All fail-safe.[0m
[7m[0m  Main\SubModule.cs:250:        var castleRecruitmentSettings = IoC.Resolve<ICastleRecruitmentSettingsProvider>();[0m
  Main\SubModule.cs:254:        Patch42_HourlyTickParty_Postfix.Initialize(castleRecruitmentSettings, logger);
> Main\SubModule.cs:255:        _harmony.[7mPatchCategory[0m("Patch42_CastleRecruitment");[0m
[7m[0m  Main\SubModule.cs:256:[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:257: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mInformationManager.DisplayMessage(new [0m[7m[0mInformationMessage("TAOM [0m[7m[0mloaded [0m[7m[0msuccessfully!", [0m
[7m[0mColors.Green));[0m
  Main\SubModule.cs:551:
> Main\SubModule.cs:552:    public override void [7mOnGameInitializationFinished[0m(Game game)[0m
[7m[0m  Main\SubModule.cs:553:    {[0m
> Main\SubModule.cs:554:        base.[7mOnGameInitializationFinished[0m(game);[0m
[7m[0m  Main\SubModule.cs:555:[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:556: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mHarmony [0m[7m[0mpatches [0m[7m[0mare [0m[7m[0mprocess-global [0m[7m[0m(applied [0m[7m[0mto [0m[7m[0mmethods, [0m[7m[0mpersist [0m[7m[0macross [0m[7m[0mgames). [0m
[7m[0mApply [0m[7m[0mthis[0m
  Main\SubModule.cs:565:
> Main\SubModule.cs:566:        _harmony.[7mPatchCategory[0m("Patch1_FirstTimeInit");[0m
> Main\SubModule.cs:567:        _harmony.[7mPatchCategory[0m("Patch2_RefreshTableau");
> Main\SubModule.cs:568:        _harmony.[7mPatchCategory[0m("Patch3_SetRace");
> Main\SubModule.cs:569:        _harmony.[7mPatchCategory[0m("Patch4_CharacterSpawner");
> Main\SubModule.cs:570:        _harmony.[7mPatchCategory[0m("Patch5_FaceGen");
> Main\SubModule.cs:571:        _harmony.[7mPatchCategory[0m("Late_Transpiler");
> Main\SubModule.cs:572:        _harmony.[7mPatchCategory[0m("Late_ActionSetOverride");
> Main\SubModule.cs:573:        _harmony.[7mPatchCategory[0m("Patch6_BannerEditor");
> Main\SubModule.cs:574:        _harmony.[7mPatchCategory[0m("Patch7_FactionMap");
> Main\SubModule.cs:575:        _harmony.[7mPatchCategory[0m("Patch9_RaceFilter");
> Main\SubModule.cs:576:        _harmony.[7mPatchCategory[0m("Patch20_NarrativeHorseGuard");
> Main\SubModule.cs:577:        _harmony.[7mPatchCategory[0m("Patch8_SiegeCampGuard");
> Main\SubModule.cs:578:        _harmony.[7mPatchCategory[0m("Patch10_WeatherBoundsGuard");
> Main\SubModule.cs:579:        _harmony.[7mPatchCategory[0m("Patch11_Diplomacy");
> Main\SubModule.cs:580:        _harmony.[7mPatchCategory[0m("Patch12_WarOfTheRing");[0m
[7m[0m  Main\SubModule.cs:581:[0m
> Main\SubModule.cs:582:        _harmony.[7mPatchCategory[0m("Patch14_Execution");
> Main\SubModule.cs:583:        _harmony.[7mPatchCategory[0m("Patch15_BannerLayerLimit");
> Main\SubModule.cs:584:        _harmony.[7mPatchCategory[0m("Patch16_AtmospherePersistence");
> Main\SubModule.cs:585:        _harmony.[7mPatchCategory[0m("Patch17_TroopWeight");
> Main\SubModule.cs:586:        _harmony.[7mPatchCategory[0m("Patch23_BannerColorPersistence");
> Main\SubModule.cs:587:        _harmony.[7mPatchCategory[0m("Patch24_BannerDriftGuard");
> Main\SubModule.cs:588:        _harmony.[7mPatchCategory[0m("Patch39_BanditPartySize");
> Main\SubModule.cs:589:        _harmony.[7mPatchCategory[0m("Patch40_HideoutDescription");
> Main\SubModule.cs:590:        _harmony.[7mPatchCategory[0m("Patch46_TournamentDwarfDismount");[0m
[7m[0m  Main\SubModule.cs:591:        // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment[0m
[7m[0m  Main\SubModule.cs:592:        // ("post-sever tick AV") was actually the CanAttack charge crash at set_attack_entity[0m
  Main\SubModule.cs:598:        Features.Spider.Hooks.Agent_Die_SpiderDismount_Patch.Initialize();
> Main\SubModule.cs:599:        _harmony.[7mPatchCategory[0m("Patch47_SpiderDeathDismount");[0m
[7m[0m  Main\SubModule.cs:600:[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:601: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mPatch48: [0m[7m[0mthe [0m[7m[0mnon-lethal [0m[7m[0msibling [0m[7m[0mof [0m[7m[0mPatch47. [0m[7m[0mA [0m[7m[0mCanDismount [0m[7m[0mmelee [0m[7m[0mhit [0m[7m[0mon [0m[7m[0ma [0m[7m[0mmounted [0m
[7m[0mSpider [0m[7m[0mRider [0m[7m[0mAVs [0m[7m[0min[0m
  Main\SubModule.cs:604:        // stays on the locked mount; damage still applies). Debugger-proven 2026-06-15. See 
docs/features/spider.md.
> Main\SubModule.cs:605:        _harmony.[7mPatchCategory[0m("Patch48_SpiderHitDismountGuard");[0m
[7m[0m  Main\SubModule.cs:606:[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:607: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mPatch50: [0m[7m[0mFinalizer [0m[7m[0mswallowing [0m[7m[0ma [0m[7m[0mvanilla [0m[7m[0mNRE [0m[7m[0min [0m[7m[0mAgent.CheckToDropFlaggedItem [0m
[7m[0m(Agent.cs:3595),[0m
  Main\SubModule.cs:611:        // OnAgentHit finish and stops the log spam. Crash report 2026-06-17. See the patch 
doc-comment.
> Main\SubModule.cs:612:        _harmony.[7mPatchCategory[0m("Patch50_DropFlaggedItemGuard");[0m
[7m[0m  Main\SubModule.cs:613:[0m
[7m[0m  Main\SubModule.cs:614:        // Patch13_RaceAge — noise reduction (NOT a crash fix). NOPs the harmless[0m
  Main\SubModule.cs:616:        // mixed-race birth (normal in TAOM). Stops the debugger break + debug-log spam.
> Main\SubModule.cs:617:        _harmony.[7mPatchCategory[0m("Patch13_RaceAge");[0m
[7m[0m  Main\SubModule.cs:618:[0m
[7m[0m  Main\SubModule.cs:619:        var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();[0m
  Main\SubModule.cs:624:        RecruitmentVM_RecruitGate_Patch.Initialize(IoC.Resolve<IOnRecruitmentResourceGate>(), 
specResLogger);
> Main\SubModule.cs:625:        _harmony.[7mPatchCategory[0m("Patch26_SpecialResources");[0m
> Main\SubModule.cs:626:        _harmony.[7mPatchCategory[0m("Patch51_RecruitmentResourceGate");
> Main\SubModule.cs:627:        _harmony.[7mPatchCategory[0m("Patch27_CareerSystem");
> Main\SubModule.cs:628:        _harmony.[7mPatchCategory[0m("Patch29_CCBodyProperties");
> Main\SubModule.cs:629:        _harmony.[7mPatchCategory[0m("Patch44_CCNameAutofill");
> Main\SubModule.cs:630:        _harmony.[7mPatchCategory[0m("Patch33_EquipPresets");
> Main\SubModule.cs:631:        _harmony.[7mPatchCategory[0m("Patch34_QuickActions");
> Main\SubModule.cs:632:        _harmony.[7mPatchCategory[0m("Patch35_CompanionTactics");
> Main\SubModule.cs:633:        _harmony.[7mPatchCategory[0m("Patch36_FiefManagement");[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:634: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m
[7m[0mSettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());[0m
> Main\SubModule.cs:635:        _harmony.[7mPatchCategory[0m("Patch38_SettlementNameplateFade");[0m
[7m[0m  Main\SubModule.cs:636:[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:637: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mPatch53_PartyIconScale [0m[7m[0m— [0m[7m[0mtranspiler [0m[7m[0mthat [0m[7m[0mrewrites [0m[7m[0mthe [0m[7m[0mtwo [0m[7m[0mhardcoded [0m[7m[0m0.3f [0m
[7m[0mcampaign-map [0m[7m[0mscale[0m
  Main\SubModule.cs:641:        
Features.PartyIconScale.Hooks.Patch53_PartyIconScale.Initialize(IoC.Resolve<IModLogger>());
> Main\SubModule.cs:642:        _harmony.[7mPatchCategory[0m("Patch53_PartyIconScale");[0m
[7m[0m  Main\SubModule.cs:643:[0m
[7m[0m  Main\SubModule.cs:644:        // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an[0m
  Main\SubModule.cs:655:        
Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.Initialize(battleLoadSvc, 
equipSnapshotAdapter);
> Main\SubModule.cs:656:        _harmony.[7mPatchCategory[0m("Patch43_BattleLoadDiagnostics");[0m
[7m[0m  Main\SubModule.cs:657:        IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();[0m
[7m[0m  Main\SubModule.cs:658:[0m
  Main\SubModule.cs:737:            _missionTimePatchesApplied = true;
> Main\SubModule.cs:738:            _harmony.[7mPatchCategory[0m("Patch_MissionTime_SetMovementOrder");[0m
[7m[0m  Main\SubModule.cs:739:        }[0m
[7m[0m  Main\SubModule.cs:740:[0m


 succeeded in 629ms:

  Main\SubModule.cs:52:using TAOM.Features.ArmyTargeting;
  Main\SubModule.cs:53:using TAOM.Features.ArmyTargeting.Models;
  Main\SubModule.cs:54:using TAOM.Features.TimeAcceleration;
> Main\SubModule.cs:55:using TAOM.Features.[7mBannerColor[0mPersistence;[0m
> Main\SubModule.cs:56:using TAOM.Features.[7mBannerColor[0mPersistence.Hooks;[0m
[7m[0m  Main\SubModule.cs:57:using TAOM.Features.LocalizationOverride;[0m
[7m[0m  Main\SubModule.cs:58:using TAOM.Features.LocalizationOverride.Hooks;[0m
[7m[0m  Main\SubModule.cs:59:using TAOM.Features.SpecialResources;[0m
[7m[0m  Main\SubModule.cs:60:using TAOM.Features.SpecialResources.Hooks;[0m
  Main\SubModule.cs:214:        // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
  Main\SubModule.cs:215:        // which is null during OnSubModuleLoad and would crash JIT prep with NRE.
  Main\SubModule.cs:216:
> Main\SubModule.cs:217:        var [7mbannerColor[0mConfig = IoC.Resolve<IBannerColorConfigProvider>();[0m
> Main\SubModule.cs:218:        var [7mbannerColor[0mService = IoC.Resolve<IBannerColorService>();[0m
[7m[0m  Main\SubModule.cs:219:        var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();[0m
[7m[0m  Main\SubModule.cs:220:[0m
> Main\SubModule.cs:221:        Banner_TryGetBannerDataFromCode_Transpiler.Initialize([7mbannerColor[0mConfig, logger);
> Main\SubModule.cs:222:        Clan_Update[7mBannerColor[0msAccordingToKingdom_Patch.Initialize(bannerColorService);
> Main\SubModule.cs:223:        Clan_Update[7mBannerColor[0msAccordingToKingdom_Patch.Initialize(logger);
> Main\SubModule.cs:224:        Clan_Update[7mBannerColor[0m_Patch.Initialize(bannerColorService, bannerHeroAdapter);
> Main\SubModule.cs:225:        Banner_GetFirstIconColor_Patch.Initialize([7mbannerColor[0mService);
> Main\SubModule.cs:226:        BannerEditorView_OnTick_Patch.Initialize([7mbannerColor[0mService, logger);
> Main\SubModule.cs:227:        CampaignUIHelper_GetCharacterCode_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:228:        SandBoxUIHelper_GetCharacterCode_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:229:        SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:230:        PartyVM_RefreshCurrentCharacterInformation_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:231:        HeroViewModel_FillFrom_Patch.Initialize([7mbannerColor[0mService, bannerHeroAdapter);
> Main\SubModule.cs:232:        PartyCharacterVM_GetCharacterCode_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:233:        ClanPartyItemVM_GetCharacterCode_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:234:        
CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Initialize([7mbannerColor[0mService);[0m
[7m[0m  Main\SubModule.cs:235:        var agentColorStore = IoC.Resolve<IAgentColorStore>();[0m
> Main\SubModule.cs:236:        Mission_SpawnAgent_Patch.Initialize([7mbannerColor[0mService, [0m[7m[0mbannerHeroAdapter, [0m
[7m[0magentColorStore);[0m
> Main\SubModule.cs:237:        Agent_EquipItemsFromSpawnEquipment_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter, [0m[7m[0magentColorStore);[0m
> Main\SubModule.cs:238:        AgentVisuals_Create_Patch.Initialize([7mbannerColor[0mService);
> Main\SubModule.cs:239:        MapConversationTableau_SpawnOpponentLeader_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:240:        MapConversationTableau_SpawnOpponentBodyguard_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:241:        [7mMobilePartyVisual_AddCharacterToPartyIcon[0m_Patch.Initialize(bannerColorService, [0m
[7m[0mbannerHeroAdapter);[0m
> Main\SubModule.cs:242:        OrderOfBattleHeroItemVM_RefreshInformation_Patch.Initialize([7mbannerColor[0mService, [0m
[7m[0mbannerHeroAdapter);[0m
[7m[0m  Main\SubModule.cs:243:[0m
[7m[0m  Main\SubModule.cs:244:        Mission_Initialize_Patch.Initialize(logger);[0m
[7m[0m  Main\SubModule.cs:245:[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:246: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0m// [0m[7m[0mPatch42_CastleRecruitment [0m[7m[0m— [0m[7m[0mcastle [0m[7m[0mnotable [0m[7m[0mrecruitment. [0m[7m[0mTargets [0m
[7m[0mRecruitmentCampaignBehavior[0m
  Main\SubModule.cs:583:        _harmony.PatchCategory("Patch15_BannerLayerLimit");
  Main\SubModule.cs:584:        _harmony.PatchCategory("Patch16_AtmospherePersistence");
  Main\SubModule.cs:585:        _harmony.PatchCategory("Patch17_TroopWeight");
> Main\SubModule.cs:586:        _harmony.PatchCategory("Patch23_[7mBannerColor[0mPersistence");[0m
[7m[0m  Main\SubModule.cs:587:        _harmony.PatchCategory("Patch24_BannerDriftGuard");[0m
[7m[0m  Main\SubModule.cs:588:        _harmony.PatchCategory("Patch39_BanditPartySize");[0m
[7m[0m  Main\SubModule.cs:589:        _harmony.PatchCategory("Patch40_HideoutDescription");[0m
[7m[0m  Main\SubModule.cs:590:        _harmony.PatchCategory("Patch46_TournamentDwarfDismount");[0m
  Main\SubModule.cs:689:            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] GetSuitableSpear not 
found — culture-specific spears will not apply");
  Main\SubModule.cs:690:
  Main\SubModule.cs:691:        // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
> Main\SubModule.cs:692:        var mobilePartyTarget = [7mMobilePartyVisual_AddCharacterToPartyIcon[0m_Patch.TargetMethod();[0m
[7m[0m  Main\SubModule.cs:693:        if (mobilePartyTarget != null)[0m
[7m[0m  Main\SubModule.cs:694:            _harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod([0m
> Main\SubModule.cs:695:                typeof([7mMobilePartyVisual_AddCharacterToPartyIcon[0m_Patch),
> Main\SubModule.cs:696:                nameof([7mMobilePartyVisual_AddCharacterToPartyIcon[0m_Patch.Postfix)));[0m
[7m[0m  Main\SubModule.cs:697:        else[0m
> Main\SubModule.cs:698:            IoC.Resolve<IModLogger>().LogWarning("[[7mBannerColor[0m] [0m
[7m[0mMobilePartyVisual.AddCharacterToPartyIcon [0m[7m[0mnot [0m[7m[0mfound [0m[7m[0m— [0m[7m[0mparty [0m[7m[0micon [0m[7m[0mcolors [0m[7m[0mwill [0m[7m[0mnot [0m[7m[0mpersist");[0m
[7m[0m  Main\SubModule.cs:699:[0m
[7m[0m  Main\SubModule.cs:700:        // Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)[0m
[7m[0m  Main\SubModule.cs:701:        var agentVisualsCreateTarget = AgentVisuals_Create_Patch.TargetMethod();[0m
[7m[0m  Main\SubModule.cs:702:        if (agentVisualsCreateTarget != null)[0m
  Main\SubModule.cs:704:                typeof(AgentVisuals_Create_Patch),
  Main\SubModule.cs:705:                nameof(AgentVisuals_Create_Patch.Prefix)));
  Main\SubModule.cs:706:        else
> Main\SubModule.cs:707:            IoC.Resolve<IModLogger>().LogWarning("[[7mBannerColor[0m] [0m[7m[0mAgentVisuals.Create [0m[7m[0mnot [0m[7m[0mfound [0m
[7m[0m— [0m[7m[0mclan [0m[7m[0mcolor [0m[7m[0mrandomness [0m[7m[0msuppression [0m[7m[0mwill [0m[7m[0mnot [0m[7m[0mapply");[0m
[7m[0m  Main\SubModule.cs:708:[0m
[7m[0m  Main\SubModule.cs:709:        // Manual patches for MapConversationTableau (private methods in SandBox.View.dll)[0m
[7m[0m  Main\SubModule.cs:710:        var leaderTarget = MapConversationTableau_SpawnOpponentLeader_Patch.TargetMethod();[0m
[7m[0m  Main\SubModule.cs:711:        if (leaderTarget != null)[0m
  Main\SubModule.cs:713:                typeof(MapConversationTableau_SpawnOpponentLeader_Patch),
  Main\SubModule.cs:714:                nameof(MapConversationTableau_SpawnOpponentLeader_Patch.Postfix)));
  Main\SubModule.cs:715:        else
> Main\SubModule.cs:716:            IoC.Resolve<IModLogger>().LogWarning("[[7mBannerColor[0m] [0m
[7m[0mMapConversationTableau.SpawnOpponentLeader [0m[7m[0mnot [0m[7m[0mfound [0m[7m[0m— [0m[7m[0mconversation [0m[7m[0mtableau [0m[7m[0mleader [0m[7m[0mcolors [0m[7m[0mwill [0m[7m[0mnot [0m[7m[0mapply");[0m
[7m[0m  Main\SubModule.cs:717:[0m
[7m [0m[7m [0m[7m[0mMain\SubModule.cs:718: [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m [0m[7m[0mvar [0m[7m[0mbodyguardTarget [0m[7m[0m= [0m
[7m[0mMapConversationTableau_SpawnOpponentBodyguard_Patch.TargetMethod();[0m
[7m[0m  Main\SubModule.cs:719:        if (bodyguardTarget != null)[0m
[7m[0m  Main\SubModule.cs:720:            _harmony.Patch(bodyguardTarget, postfix: new HarmonyMethod([0m
  Main\SubModule.cs:721:                typeof(MapConversationTableau_SpawnOpponentBodyguard_Patch),
  Main\SubModule.cs:722:                nameof(MapConversationTableau_SpawnOpponentBodyguard_Patch.Postfix)));
  Main\SubModule.cs:723:        else
> Main\SubModule.cs:724:            IoC.Resolve<IModLogger>().LogWarning("[[7mBannerColor[0m] [0m
[7m[0mMapConversationTableau.SpawnOpponentBodyguardCharacter [0m[7m[0mnot [0m[7m[0mfound [0m[7m[0m— [0m[7m[0mconversation [0m[7m[0mtableau [0m[7m[0mbodyguard [0m[7m[0mcolors [0m[7m[0mwill [0m[7m[0mnot [0m
[7m[0mapply");[0m
[7m[0m  Main\SubModule.cs:725:    }[0m
[7m[0m  Main\SubModule.cs:726:[0m
[7m[0m  Main\SubModule.cs:727:    public override void OnMissionBehaviorInitialize(Mission mission)[0m
[7m[0m  Main\SubModule.cs:728:    {[0m


 succeeded in 582ms:

Main\Features\TaomSettings.cs:572:    public float [7mMapFigureScale[0m { get; set; } = 0.15f;


codex
Registration is in `OnGameInitializationFinished`, behind the existing once-per-process guard for game-init patches, and the BannerColor postfix is patched manually after category patching in the same method. I’m going to do a tighter search for all feature references and, if possible, inspect decompiled vanilla artifacts directly rather than relying only on the supplied IL excerpt.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main','TAOM.Tests','docs' -Recurse -File | Select-String -Pattern 'PartyIconScale|Patch53_PartyIconScale|MapFigureScale' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*MobilePartyVisual*' -ErrorAction SilentlyContinue | Select-Object -First 20 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -File | Select-String -Pattern 'HarmonyPatchCategory\\(\"Patch23_BannerColorPersistence\"|MobilePartyVisual_AddCharacterToPartyIcon_Patch' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*AgentVisualsData*' -ErrorAction SilentlyContinue | Select-Object -First 20 FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 515ms:
 succeeded in 551ms:

[32;1mFullName                                                                                                    [0m[32;1m Length[0m
[32;1m--------                                                                                                    [0m [32;1m------[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs   8269


2026-06-24T14:33:21.303927Z ERROR codex_core::tools::router: error=Exit code: 124
Wall time: 30.2 seconds
Output:
command timed out after 30160 milliseconds

[32;1mPath                                                           [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                           [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     637         // Patch53_PartyIconScale — transpi…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     639         // to PartyIconScaleConfig.GetScale…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     641         Features.PartyIconScale.Hooks.Patch…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     642         _harmony.PatchCategory("Patch53_Par…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.dll      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.dll      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.pdb       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs         572     public float MapFigureScale { get; set;…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          3 namespace TAOM.Features.PartyIconScale;
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          7 /// (<see cref="TaomSettings.MapFigureScale…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          9 /// <see cref="GetScale"/> is the static th…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         23 public static class PartyIconScaleConfig
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         47     public static float GetScale() => Resol…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          8 namespace TAOM.Features.PartyIconScale;
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         11 /// IL surgery for <c>Patch53_PartyIconScal…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         12 /// <c>MobilePartyVisual.AddCharacterToPart…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         26 internal static class PartyIconScaleTranspi…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         36                 "[PartyIconScale] GetScale …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         42                 "[PartyIconScale] people sc…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         46                 "[PartyIconScale] mount sca…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          7 namespace TAOM.Features.PartyIconScale.Hook…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         12 /// <c>call</c> to <see cref="PartyIconScal…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         14 /// <see cref="PartyIconScaleTranspiler"/>.…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         17 [HarmonyPatchCategory("Patch53_PartyIconSca…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         18 public static class Patch53_PartyIconScale
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         26         _getScale = AccessTools.Method(type…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         34         PartyIconScaleTranspiler.Rewrite(in…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.dll      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.dll      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.pdb       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Deskt…      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Deskt…      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Deskt…       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shippi…      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shippi…      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shippi…       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…

 exited 124 in 30160ms:

[32;1mPath                                                           [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                           [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     637         // Patch53_PartyIconScale — transpi…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     639         // to PartyIconScaleConfig.GetScale…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     641         Features.PartyIconScale.Hooks.Patch…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                     642         _harmony.PatchCategory("Patch53_Par…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.dll      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.dll      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.pdb       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs         572     public float MapFigureScale { get; set;…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          3 namespace TAOM.Features.PartyIconScale;
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          7 /// (<see cref="TaomSettings.MapFigureScale…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          9 /// <see cref="GetScale"/> is the static th…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         23 public static class PartyIconScaleConfig
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         47     public static float GetScale() => Resol…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          8 namespace TAOM.Features.PartyIconScale;
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         11 /// IL surgery for <c>Patch53_PartyIconScal…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         12 /// <c>MobilePartyVisual.AddCharacterToPart…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         26 internal static class PartyIconScaleTranspi…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         36                 "[PartyIconScale] GetScale …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         42                 "[PartyIconScale] people sc…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         46                 "[PartyIconScale] mount sca…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…          7 namespace TAOM.Features.PartyIconScale.Hook…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         12 /// <c>call</c> to <see cref="PartyIconScal…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         14 /// <see cref="PartyIconScaleTranspiler"/>.…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         17 [HarmonyPatchCategory("Patch53_PartyIconSca…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         18 public static class Patch53_PartyIconScale
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         26         _getScale = AccessTools.Method(type…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\…         34         PartyIconScaleTranspiler.Rewrite(in…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.dll      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.dll      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.pdb       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Deskt…      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Deskt…      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Deskt…       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shippi…      34795  \  \  \  � i     <Clone>$ <.cctor>b_…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shippi…      36647 OnFinalize  �� ��TaleWorlds.CampaignSystem…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shippi…       2763 � h�����\%��J���#[����Y�"�I��) ���NavalTra…

2026-06-24T14:33:21.396876Z ERROR codex_core::tools::router: error=Exit code: 124
Wall time: 30.2 seconds
Output:
command timed out after 30186 milliseconds

[32;1mPath                                                               [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                               [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         241         MobilePartyVisual_AddCharacterT…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         692         var mobilePartyTarget = MobileP…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         695                 typeof(MobilePartyVisua…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         696                 nameof(MobilePartyVisua…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.dll          34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.pdb           2568 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\bin\Release\net472\TAOM.dll        31423  \  \  \  � a     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\bin\Release\net472\TAOM.pdb         2945 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\bin\x64\Debug\net472\TAOM.dll      33647  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\bin\x64\Debug\net472\TAOM.pdb       2554 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          7 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         10 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         12 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         10 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         16 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         12 public static class MobilePartyVisual_A…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         12 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         10 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.dll          34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.pdb           2568 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\obj\Release\net472\TAOM.dll        31423  \  \  \  � a     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\obj\Release\net472\TAOM.pdb         2945 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\obj\x64\Debug\net472\TAOM.dll      33647  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\obj\x64\Debug\net472\TAOM.pdb       2554 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Desktop.x…      34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Desktop.x…       2568 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shipping_C…      34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shipping_C…       2568 �̮���������D^�m\q�O[���3MapConversat…

 exited 124 in 30186ms:

[32;1mPath                                                               [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                               [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         241         MobilePartyVisual_AddCharacterT…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         692         var mobilePartyTarget = MobileP…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         695                 typeof(MobilePartyVisua…
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs                         696                 nameof(MobilePartyVisua…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.dll          34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\bin\Debug\net472\TAOM.pdb           2568 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\bin\Release\net472\TAOM.dll        31423  \  \  \  � a     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\bin\Release\net472\TAOM.pdb         2945 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\bin\x64\Debug\net472\TAOM.dll      33647  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\bin\x64\Debug\net472\TAOM.pdb       2554 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          7 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         10 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         12 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         10 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         16 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         12 public static class MobilePartyVisual_A…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         12 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…         10 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersiste…          9 [HarmonyPatchCategory("Patch23_BannerCo…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.dll          34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\obj\Debug\net472\TAOM.pdb           2568 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\obj\Release\net472\TAOM.dll        31423  \  \  \  � a     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\obj\Release\net472\TAOM.pdb         2945 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\obj\x64\Debug\net472\TAOM.dll      33647  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\obj\x64\Debug\net472\TAOM.pdb       2554 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Desktop.x…      34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Gaming.Desktop.x…       2568 �̮���������D^�m\q�O[���3MapConversat…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shipping_C…      34795  \  \  \  � i     <Clone>$ <.ccto…
C:\Users\mikew\source\repos\TAOM\Main\_Module\bin\Win64_Shipping_C…       2568 �̮���������D^�m\q�O[���3MapConversat…

codex
The broad recursive search hit compiled `bin/obj` artifacts and timed out, so I’m narrowing it to source folders only. I did find decompiled `AgentVisualsData.cs`; next I’m locating `MobilePartyVisual.cs` under the decompiled SandBox.View tree.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Directory | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\AgentVisualsData.cs' -Pattern 'Scale\\(' -Context 2,4" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features','TAOM.Tests/Features','docs/features' -Recurse -File -Include *.cs,*.md | Select-String -Pattern 'PartyIconScale|Patch53_PartyIconScale|MapFigureScale' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/BannerColorPersistence' -Recurse -File -Include *.cs | Select-String -Pattern 'HarmonyPatchCategory\\(\"Patch23_BannerColorPersistence\"|MobilePartyVisual_AddCharacterToPartyIcon_Patch' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 564ms:

[32;1mName                  [0m[32;1m FullName[0m
[32;1m----                  [0m [32;1m--------[0m
_editor_build          E:\Decompiled_Bannerlord\_editor_build
_shipping_build        E:\Decompiled_Bannerlord\_shipping_build
_shipping_build_v1.4.5 E:\Decompiled_Bannerlord\_shipping_build_v1.4.5
Campaign               E:\Decompiled_Bannerlord\Campaign
Core                   E:\Decompiled_Bannerlord\Core
Engine                 E:\Decompiled_Bannerlord\Engine
Modules                E:\Decompiled_Bannerlord\Modules
MountAndBlade          E:\Decompiled_Bannerlord\MountAndBlade
Network                E:\Decompiled_Bannerlord\Network
Platform               E:\Decompiled_Bannerlord\Platform
ThirdParty             E:\Decompiled_Bannerlord\ThirdParty
UI                     E:\Decompiled_Bannerlord\UI


 succeeded in 564ms:

  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs:269:	}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs:270:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs:271:	
public AgentVisualsData [7mScale([0mfloat [0m[7m[0mscale)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs:272:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs:273:	[0m[7m	[0m
[7m[0mScaleData [0m[7m[0m= [0m[7m[0mscale;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs:274:	[0m[7m	[0m
[7m[0mreturn [0m[7m[0mthis;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVisualsData.cs:275:	}[0m


 succeeded in 514ms:

[32;1mPath[0m
[32;1m----                                                                                                                   [0m
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Agent_EquipItemsFromSpawnEquipment_Patch.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Banner_GetFirstIconColor_Patch.cs          
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\BannerEditorView_OnTick_Patch.cs           
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\CampaignSceneNotificationHelper_CreateNoti…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\CampaignUIHelper_GetCharacterCode_Patch.cs 
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\ClanPartyItemVM_GetCharacterCode_Patch.cs  
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\HeroViewModel_FillFrom_Patch.cs            
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\Mission_SpawnAgent_Patch.cs                
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\MobilePartyVisual_AddCharacterToPartyIcon_…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\OrderOfBattleHeroItemVM_RefreshInformation…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\PartyCharacterVM_GetCharacterCode_Patch.cs 
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\PartyVM_RefreshCurrentCharacterInformation…
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\SandBoxUIHelper_GetCharacterCode_Patch.cs  
C:\Users\mikew\source\repos\TAOM\Main\Features\BannerColorPersistence\Hooks\SPInventoryVM_UpdateCurrentCharacterIfPoss…


 succeeded in 803ms:

[32;1mPath                                                                                                [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                                [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                 7 namesp…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                12 /// <c…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                14 /// <s…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                17 [Harmo…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                18 public…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                26       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                34       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                         3 namesp…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                         7 /// (<…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                         9 /// <s…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        23 public…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        47     pu…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                     8 namesp…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    11 /// IL…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    12 /// <c…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    26 intern…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    36       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    42       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    46       …
C:\Users\mikew\source\repos\TAOM\Main\Features\TaomSettings.cs                                              572     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs              2 using …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs              4 namesp…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs              7 public…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             14       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             20       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             21       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             27       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             28       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             34       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             35       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             41       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             47       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             53       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             54       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             60       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             61       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             67       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             73       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs          8 using …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         10 namesp…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         13 public…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         16       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         23       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         40       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         56       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         78       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         98       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs        113       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs        128       …
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           24 value …
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           42 Patch5…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           44 PartyI…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           46 PartyI…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           48 TaomSe…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           60 Valida…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           69 | `Mai…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           70 | `Mai…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           71 | `Mai…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           72 | `Mai…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           73 | `Mai…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           84 | `TAO…
C:\Users\mikew\source\repos\TAOM\docs\features\party-icon-scale.md                                           85 | `TAO…


codex
The decompiled `AgentVisualsData.Scale(float)` body is exactly the expected one-argument fluent setter. I found no extra source consumers beyond the MCM property, static config, transpiler, registration, docs, and tests; I’m now looking at the decompiled `MobilePartyVisual` method itself for target and overload evidence.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\_shipping_build' -Directory | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Modules\\SandBox' -Directory -ErrorAction SilentlyContinue | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Modules' -Directory | Where-Object { "'$_.Name -like '"'*Sand*' } | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\_shipping_build' -Recurse -Filter 'MobilePartyVisual.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 516ms:
 succeeded in 460ms:
 succeeded in 520ms:

[32;1mName   [0m[32;1m FullName[0m
[32;1m----   [0m [32;1m--------[0m
SandBox E:\Decompiled_Bannerlord\Modules\SandBox


 succeeded in 557ms:

[32;1mName                                             [0m[32;1m FullName[0m
[32;1m----                                             [0m [32;1m--------[0m
Properties                                        E:\Decompiled_Bannerlord\Modules\SandBox\Properties
Sandbox                                           E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox
SandBox.AI                                        E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.AI
SandBox.BoardGames                                E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames
SandBox.BoardGames.AI                             E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.AI
SandBox.BoardGames.MissionLogics                  E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.MissionL…
SandBox.BoardGames.Objects                        E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.Objects
SandBox.BoardGames.Pawns                          E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.Pawns
SandBox.BoardGames.Tiles                          E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.Tiles
SandBox.CampaignBehaviors                         E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors
SandBox.Conversation                              E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Conversation
SandBox.Conversation.MissionLogics                E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Conversation.Missio…
SandBox.GameComponents                            E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.GameComponents
SandBox.Issues                                    E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues
SandBox.Issues.IssueQuestTasks                    E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues.IssueQuestTa…
SandBox.Missions                                  E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions
SandBox.Missions.AgentBehaviors                   E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.AgentBehav…
SandBox.Missions.BattleScore                      E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.BattleScore
SandBox.Missions.MissionEvents                    E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionEve…
SandBox.Missions.MissionLogics                    E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLog…
SandBox.Missions.MissionLogics.Arena              E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLog…
SandBox.Missions.MissionLogics.Hideout            E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLog…
SandBox.Missions.MissionLogics.Hideout.Objectives E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLog…
SandBox.Missions.MissionLogics.Towns              E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLog…
SandBox.Objects                                   E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Objects
SandBox.Objects.AnimationPoints                   E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Objects.AnimationPo…
SandBox.Objects.AreaMarkers                       E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Objects.AreaMarkers
SandBox.Objects.Cinematics                        E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Objects.Cinematics
SandBox.Objects.Usables                           E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Objects.Usables
SandBox.Source.Missions.AgentBehaviors            E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Source.Missions.Age…
SandBox.Tournaments                               E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments
SandBox.Tournaments.AgentControllers              E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments.AgentCo…
SandBox.Tournaments.MissionLogics                 E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments.Mission…


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Modules\\SandBox' -Recurse -Filter 'MobilePartyVisual.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Modules\\SandBox' -Recurse -File -Include '*PartyVisual*.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Directory -Filter 'SandBox.View' -ErrorAction SilentlyContinue | Select-Object -First 20 FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 554ms:
 succeeded in 577ms:
 succeeded in 561ms:
codex
The decompiled tree here appears to omit `SandBox.View` source files even though the user supplied the verified IL. I’m still checking available installed assemblies/tools so I can corroborate target shape where possible; otherwise I’ll explicitly base the IL-specific conclusions on the provided v1.4.6 IL excerpt plus the local patch code.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\_shipping_build' -Force | Select-Object -First 50 Name,FullName,PSIsContainer,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Item -Path 'E:\\Decompiled_Bannerlord\\_shipping_build' | Select-Object FullName,PSIsContainer,Length,Attributes" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Command ilspycmd -ErrorAction SilentlyContinue | Select-Object Source,Version' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord' -Recurse -Filter 'SandBox.View.dll' -ErrorAction SilentlyContinue | Select-Object -First 10 FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 552ms:

[32;1mFullName                                [0m[32;1m PSIsContainer[0m[32;1m Length[0m[32;1m Attributes[0m
[32;1m--------                                [0m [32;1m-------------[0m [32;1m------[0m [32;1m----------[0m
E:\Decompiled_Bannerlord\_shipping_build          True         Directory


 succeeded in 559ms:

[32;1mName                                            [0m[32;1m FullName[0m
[32;1m----                                            [0m [32;1m--------                                                              [0m
_native_dlls.txt                                 E:\Decompiled_Bannerlord\_shipping_build\_native_dlls.txt             
GalaxyCSharp.cs                                  E:\Decompiled_Bannerlord\_shipping_build\GalaxyCSharp.cs              
jose-jwt.cs                                      E:\Decompiled_Bannerlord\_shipping_build\jose-jwt.cs                  
Newtonsoft.Json.cs                               E:\Decompiled_Bannerlord\_shipping_build\Newtonsoft.Json.cs           
Steamworks.NET.cs                                E:\Decompiled_Bannerlord\_shipping_build\Steamworks.NET.cs            
System.Management.cs                             E:\Decompiled_Bannerlord\_shipping_build\System.Management.cs         
System.Numerics.Vectors.cs                       E:\Decompiled_Bannerlord\_shipping_build\System.Numerics.Vectors.cs   
TaleWorlds.AchievementSystem.cs                  E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.AchievementSystem…
TaleWorlds.ActivitySystem.cs                     E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.ActivitySystem.cs 
TaleWorlds.CampaignSystem.cs                     E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs 
TaleWorlds.CampaignSystem.ViewModelCollection.cs E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.Vi…
TaleWorlds.Core.cs                               E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Core.cs           
TaleWorlds.Core.ViewModelCollection.cs           E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Core.ViewModelCol…
TaleWorlds.Diamond.AccessProvider.GDK.cs         E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Diamond.AccessPro…
TaleWorlds.Diamond.AccessProvider.GOG.cs         E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Diamond.AccessPro…
TaleWorlds.Diamond.AccessProvider.Steam.cs       E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Diamond.AccessPro…
TaleWorlds.Diamond.AccessProvider.Test.cs        E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Diamond.AccessPro…
TaleWorlds.Diamond.cs                            E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Diamond.cs        
TaleWorlds.DotNet.AutoGenerated.cs               E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.DotNet.AutoGenera…
TaleWorlds.DotNet.cs                             E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.DotNet.cs         
TaleWorlds.Engine.AutoGenerated.cs               E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Engine.AutoGenera…
TaleWorlds.Engine.cs                             E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Engine.cs         
TaleWorlds.Engine.GauntletUI.cs                  E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Engine.GauntletUI…
TaleWorlds.GauntletUI.CodeGenerator.cs           E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.CodeGe…
TaleWorlds.GauntletUI.cs                         E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.cs     
TaleWorlds.GauntletUI.Data.cs                    E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.Data.cs
TaleWorlds.GauntletUI.ExtraWidgets.cs            E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.ExtraW…
TaleWorlds.GauntletUI.PrefabSystem.cs            E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.Prefab…
TaleWorlds.InputSystem.cs                        E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.InputSystem.cs    
TaleWorlds.Library.cs                            E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Library.cs        
TaleWorlds.LinQuick.cs                           E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.LinQuick.cs       
TaleWorlds.Localization.cs                       E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Localization.cs   
TaleWorlds.ModuleManager.cs                      E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.ModuleManager.cs  
TaleWorlds.MountAndBlade.AutoGenerated.cs        E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Aut…
TaleWorlds.MountAndBlade.cs                      E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.cs  
TaleWorlds.MountAndBlade.Diamond.cs              E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Dia…
TaleWorlds.MountAndBlade.GauntletUI.Widgets.cs   E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Gau…
TaleWorlds.MountAndBlade.Helpers.cs              E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Hel…
TaleWorlds.MountAndBlade.Launcher.Library.cs     E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Lau…
TaleWorlds.MountAndBlade.Launcher.Steam.cs       E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Lau…
TaleWorlds.MountAndBlade.Multiplayer.Test.cs     E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Mul…
TaleWorlds.MountAndBlade.ViewModelCollection.cs  E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.Vie…
TaleWorlds.NavigationSystem.cs                   E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.NavigationSystem.…
TaleWorlds.Network.cs                            E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.Network.cs        
TaleWorlds.ObjectSystem.cs                       E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.ObjectSystem.cs   
TaleWorlds.PlatformService.cs                    E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.PlatformService.cs
TaleWorlds.PlatformService.Epic.cs               E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.PlatformService.E…
TaleWorlds.PlatformService.GOG.cs                E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.PlatformService.G…
TaleWorlds.PlatformService.Steam.cs              E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.PlatformService.S…
TaleWorlds.PlayerServices.cs                     E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.PlayerServices.cs 


 succeeded in 535ms:

[32;1mSource                                   [0m[32;1m Version[0m
[32;1m------                                   [0m [32;1m-------[0m
C:\Users\mikew\.dotnet\tools\ilspycmd.exe 10.0.1.8346


 exited 1 in 507ms:
2026-06-24T14:34:05.755249Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\_shipping_build' -Filter '*SandBox.View*' | Select-Object Name,FullName,Length" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\_shipping_build\\SandBox.View.cs' -Pattern 'class MobilePartyVisual|AddCharacterToPartyIcon|0\\.3f|Scale\\(' -Context 2,4 -ErrorAction SilentlyContinue" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'C:\\' -Recurse -Filter 'SandBox.View.dll' -ErrorAction SilentlyContinue | Select-Object -First 20 FullName,Length" in C:\Users\mikew\source\repos\TAOM
 succeeded in 559ms:
2026-06-24T14:34:14.927293Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path 'E:\Decompiled_Bannerlord\_shipping_build\SandBox[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\_shipping_build\SandBox.View.cs' because it does not exist.[0m

 exited 1 in 644ms:
[31;1mSelect-String: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mSelect-String -Path 'E:\Decompiled_Bannerlord\_shipping_build\SandBox[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\_shipping_build\SandBox.View.cs' because it does not exist.[0m

2026-06-24T14:35:03.775222Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 49.4 seconds
Output:

 exited 1 in 49385ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/SubModule.cs' | ForEach-Object { "'$n += 1; if ($n -ge 548 -and $n -le 646) { '"'{0}:{1}' -f "'$n, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$n=0; Get-Content -Path '"'Main/SubModule.cs' | ForEach-Object { "'$n += 1; if ($n -ge 686 -and $n -le 700) { '"'{0}:{1}' -f "'$n, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/BannerColorPersistence/Hooks' -File -Include *.cs | ForEach-Object { Select-String -Path "'$_.FullName -Pattern '"'MobilePartyVisual_AddCharacterToPartyIcon_Patch|HarmonyPatchCategory|HarmonyPatch\\(' -Context 1,1 }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 527ms:
686:                typeof(GuardsCampaignBehavior_GetSuitableSpear_Patch),
687:                nameof(GuardsCampaignBehavior_GetSuitableSpear_Patch.Prefix)));
688:        else
689:            IoC.Resolve<IModLogger>().LogWarning("[SettlementGuards] GetSuitableSpear not found — culture-specific spears will not apply");
690:
691:        // Manual patch for private MobilePartyVisual method (SandBox.View.dll)
692:        var mobilePartyTarget = MobilePartyVisual_AddCharacterToPartyIcon_Patch.TargetMethod();
693:        if (mobilePartyTarget != null)
694:            _harmony.Patch(mobilePartyTarget, postfix: new HarmonyMethod(
695:                typeof(MobilePartyVisual_AddCharacterToPartyIcon_Patch),
696:                nameof(MobilePartyVisual_AddCharacterToPartyIcon_Patch.Postfix)));
697:        else
698:            IoC.Resolve<IModLogger>().LogWarning("[BannerColor] MobilePartyVisual.AddCharacterToPartyIcon not found — party icon colors will not persist");
699:
700:        // Manual patch for AgentVisuals.Create (TaleWorlds.MountAndBlade.View.dll)

 succeeded in 533ms:
548:                IoC.Resolve<IModLogger>()));
549:        }
550:    }
551:
552:    public override void OnGameInitializationFinished(Game game)
553:    {
554:        base.OnGameInitializationFinished(game);
555:
556:        // Harmony patches are process-global (applied to methods, persist across games). Apply this
557:        // whole per-game-init patch block ONCE per process — re-applying on a 2nd game init duplicates
558:        // every prefix/postfix, restarts the BattleLoad watchdog, and CRASHES the non-idempotent
559:        // DeliverOffSpring transpiler (chained twice, it can't find its already-NOPped anchor). The
560:        // shader-precompile walk starts N custom games in one process and tripped exactly this on item 2;
561:        // a player loading a 2nd campaign/custom-battle in one session hits the same crash.
562:        // Mirrors _missionTimePatchesApplied in OnMissionBehaviorInitialize.
563:        if (_gameInitPatchesApplied) return;
564:        _gameInitPatchesApplied = true;
565:
566:        _harmony.PatchCategory("Patch1_FirstTimeInit");
567:        _harmony.PatchCategory("Patch2_RefreshTableau");
568:        _harmony.PatchCategory("Patch3_SetRace");
569:        _harmony.PatchCategory("Patch4_CharacterSpawner");
570:        _harmony.PatchCategory("Patch5_FaceGen");
571:        _harmony.PatchCategory("Late_Transpiler");
572:        _harmony.PatchCategory("Late_ActionSetOverride");
573:        _harmony.PatchCategory("Patch6_BannerEditor");
574:        _harmony.PatchCategory("Patch7_FactionMap");
575:        _harmony.PatchCategory("Patch9_RaceFilter");
576:        _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
577:        _harmony.PatchCategory("Patch8_SiegeCampGuard");
578:        _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
579:        _harmony.PatchCategory("Patch11_Diplomacy");
580:        _harmony.PatchCategory("Patch12_WarOfTheRing");
581:
582:        _harmony.PatchCategory("Patch14_Execution");
583:        _harmony.PatchCategory("Patch15_BannerLayerLimit");
584:        _harmony.PatchCategory("Patch16_AtmospherePersistence");
585:        _harmony.PatchCategory("Patch17_TroopWeight");
586:        _harmony.PatchCategory("Patch23_BannerColorPersistence");
587:        _harmony.PatchCategory("Patch24_BannerDriftGuard");
588:        _harmony.PatchCategory("Patch39_BanditPartySize");
589:        _harmony.PatchCategory("Patch40_HideoutDescription");
590:        _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
591:        // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment
592:        // ("post-sever tick AV") was actually the CanAttack charge crash at set_attack_entity
593:        // (0x6BAB4E), which fired with AND without Patch47 and is fixed in data (LOTRLOME
594:        // monster Flags). Patch47's own job verified working: severed riders die clean on-foot
595:        // deaths (act_death_by_arrow class) instead of AVing in the native mounted-death path —
596:        // which 1.4.6 still does on melee deaths (Die-path AV reading float-bits-as-index from
597:        // a corrupted action record, debugger-proven 06-12). See docs/features/spider.md.
598:        Features.Spider.Hooks.Agent_Die_SpiderDismount_Patch.Initialize();
599:        _harmony.PatchCategory("Patch47_SpiderDeathDismount");
600:
601:        // Patch48: the non-lethal sibling of Patch47. A CanDismount melee hit on a mounted Spider Rider AVs in
602:        // native HandleBlowAux (reading 0x3) — the same broken non-vanilla mounted-dismount path Patch47 routes
603:        // around on death. Strips CanDismount for spider riders so the native dismount never fires (the rider
604:        // stays on the locked mount; damage still applies). Debugger-proven 2026-06-15. See docs/features/spider.md.
605:        _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
606:
607:        // Patch50: Finalizer swallowing a vanilla NRE in Agent.CheckToDropFlaggedItem (Agent.cs:3595),
608:        // reached via the shared synthetic-bite path (CustomAttacksUtils.TakeDamage → RegisterBlow →
609:        // OnAgentHit → affectedAgent.CheckToDropFlaggedItem) when a warg bites another warg (mount
610:        // victim with a null wielded Item). Already caught by WargAttackService, but swallowing lets
611:        // OnAgentHit finish and stops the log spam. Crash report 2026-06-17. See the patch doc-comment.
612:        _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
613:
614:        // Patch13_RaceAge — noise reduction (NOT a crash fix). NOPs the harmless
615:        // mother.Race == father.Race SilentAssert in DeliverOffSpring that fires on every
616:        // mixed-race birth (normal in TAOM). Stops the debugger break + debug-log spam.
617:        _harmony.PatchCategory("Patch13_RaceAge");
618:
619:        var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();
620:        var specResLogger = IoC.Resolve<IModLogger>();
621:        PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
622:        PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
623:        PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
624:        RecruitmentVM_RecruitGate_Patch.Initialize(IoC.Resolve<IOnRecruitmentResourceGate>(), specResLogger);
625:        _harmony.PatchCategory("Patch26_SpecialResources");
626:        _harmony.PatchCategory("Patch51_RecruitmentResourceGate");
627:        _harmony.PatchCategory("Patch27_CareerSystem");
628:        _harmony.PatchCategory("Patch29_CCBodyProperties");
629:        _harmony.PatchCategory("Patch44_CCNameAutofill");
630:        _harmony.PatchCategory("Patch33_EquipPresets");
631:        _harmony.PatchCategory("Patch34_QuickActions");
632:        _harmony.PatchCategory("Patch35_CompanionTactics");
633:        _harmony.PatchCategory("Patch36_FiefManagement");
634:        SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
635:        _harmony.PatchCategory("Patch38_SettlementNameplateFade");
636:
637:        // Patch53_PartyIconScale — transpiler that rewrites the two hardcoded 0.3f campaign-map scale
638:        // literals in MobilePartyVisual.AddCharacterToPartyIcon (leader figure + its mount) into a call
639:        // to PartyIconScaleConfig.GetScale(), so both honour the MCM "Map Figure Scale" slider
640:        // (default 0.15 = half vanilla). See docs/features/party-icon-scale.md.
641:        Features.PartyIconScale.Hooks.Patch53_PartyIconScale.Initialize(IoC.Resolve<IModLogger>());
642:        _harmony.PatchCategory("Patch53_PartyIconScale");
643:
644:        // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an
645:        // intermittent battle-load hang leaves a log whose last line names the stuck phase
646:        // (and, for the equipment phase, the agent + the item whose bo_ collision mesh is

 succeeded in 471ms:
codex
At this point I haven’t found an implementation defect. I’m running the focused PartyIconScale tests now to catch any harness-level problem, then I’ll finalize with confirmed/refuted verdicts and any residual test gaps.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path '.' -File -Include *.sln,*.csproj | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path '.' -Recurse -Filter '*.csproj' | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 505ms:
2026-06-24T14:36:36.000162Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.9 seconds
Output:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderE…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtende…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Mono.Cecil.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Mono.Cecil.Rocks.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Test\Mono.Cecil.Rocks.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Mono.Cecil.Mdb.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Test\Mono.Cecil.Mdb.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Mono.Cecil.Pdb.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Test\Mono.Cecil.Pdb.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Test\Mono.Cecil.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\Documentation.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\TestLibrary\TestLibrary.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod\MonoMod.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.DebugIL\MonoMod.DebugIL.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour\MonoMod.RuntimeDetou…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour.HookGen\MonoMod.Runt…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.Utils\MonoMod.Utils.csproj
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj

[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fft9w7e' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fyfj2_j' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp29jamgyn' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp2owwhlqy' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp5516wbdv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp78qze76y' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp8gds9xfp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpbwbm4o8d' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpc85yzrbw' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpi3maldm8' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpkr2gpims' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpktgkfgay' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplcnrd81l' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplorpih3w' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpm39f2s15' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpov4yy7nx' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpsanxk9fp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpwy0s04pv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpyqy6lg5o' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp2\tmpulsj5ei5' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

 exited 1 in 853ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\TAOM.Dependencies.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderE…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtende…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Mono.Cecil.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Mono.Cecil.Rocks.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Test\Mono.Cecil.Rocks.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Mono.Cecil.Mdb.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Test\Mono.Cecil.Mdb.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Mono.Cecil.Pdb.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Test\Mono.Cecil.Pdb.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Test\Mono.Cecil.Tests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\Documentation.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\TestLibrary\TestLibrary.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod\MonoMod.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.DebugIL\MonoMod.DebugIL.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour\MonoMod.RuntimeDetou…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour.HookGen\MonoMod.Runt…
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.Utils\MonoMod.Utils.csproj
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj
C:\Users\mikew\source\repos\TAOM\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj

[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fft9w7e' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp1fyfj2_j' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp29jamgyn' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp2owwhlqy' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp5516wbdv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp78qze76y' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmp8gds9xfp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpbwbm4o8d' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpc85yzrbw' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpi3maldm8' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpkr2gpims' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpktgkfgay' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplcnrd81l' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmplorpih3w' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpm39f2s15' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpov4yy7nx' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpsanxk9fp' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpwy0s04pv' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp\tmpyqy6lg5o' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.codex_tmp2\tmpulsj5ei5' is denied.[0m
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path '.' -Recurse -Filter '*.csproj'[0m | Select-Object F …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

codex
The project scan found `TAOM.Tests/TAOM.Tests.csproj`; I’m invoking only tests with `PartyIconScale` in the name. The earlier recursive project search hit denied temp folders, but it still found the main test project.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "dotnet test 'TAOM.Tests/TAOM.Tests.csproj' --filter PartyIconScale --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-24T14:36:41.777858Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 500ms:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

codex
The first test invocation failed before build/test execution because `dotnet` tried to write first-run sentinel files under a sandboxed profile. I’ll retry with `DOTNET_CLI_HOME` pointed at `C:\tmp`, which is writable.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$env:DOTNET_CLI_HOME='"'C:\\tmp\\dotnet-cli-home'; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test 'TAOM.Tests/TAOM.Tests.csproj' --filter PartyIconScale --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-24T14:36:47.643546Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
Failed to add 'C:\tmp\dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet-cli-home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 450ms:
Failed to add 'C:\tmp\dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet-cli-home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

codex
`dotnet` still cannot create its first-run directory even under `C:\tmp`, so the test runner is blocked by environment permissions rather than test failures. I’ll note that in the gate and continue the static review.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Test-Path 'C:\\tmp'; Get-Acl 'C:\\tmp' | Format-List Path,Owner,AccessToString" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'C:\\tmp' -Force -ErrorAction SilentlyContinue | Select-Object -First 20 Name,Attributes" in C:\Users\mikew\source\repos\TAOM
 succeeded in 455ms:

[32;1mName                          [0m[32;1m Attributes[0m
[32;1m----                          [0m [32;1m----------[0m
bl-harmony-decompiled           Directory
claude                          Directory
harmony-decompiled              Directory
uiextenderex_src                Directory
uiextenderex-decompiled         Directory
AbilityCatalogService.cs          Archive
common_lords.txt                  Archive
CraftingPieceCatalogService.cs    Archive
CrossReferenceService.cs          Archive
FactionCatalogService.cs          Archive
final_report.txt                  Archive
find_common_lords.ps1             Archive
find_missing_lords.ps1            Archive
gen_remaining_careers.py          Archive
generate_lord_templates.ps1       Archive
is_female_template.xslt           Archive
ItemCatalogService.cs             Archive
ItemTraitCatalogService.cs        Archive
IValidationService.cs             Archive
lotraom_lords.txt                 Archive


 succeeded in 773ms:
True

[32;1mPath           : [0mMicrosoft.PowerShell.Core\FileSystem::C:\tmp
[32;1mOwner          : [0mMike_Desktop\mikew
[32;1mAccessToString : [0mBUILTIN\Administrators Allow  FullControl
                 NT AUTHORITY\SYSTEM Allow  FullControl
                 BUILTIN\Users Allow  ReadAndExecute, Synchronize
                 NT AUTHORITY\Authenticated Users Allow  Modify, Synchronize
                 NT AUTHORITY\Authenticated Users Allow  -536805376


codex
`C:\tmp` exists but ACLs are not usable by this sandbox identity for creating the `.dotnet` subfolder. I’m going to make a local workspace dotnet home and retry once; if that is blocked too, I’ll stop testing.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "New-Item -ItemType Directory -Force -Path '.codex_dotnet_home' | Out-Null; "'$env:DOTNET_CLI_HOME=(Resolve-Path '"'.codex_dotnet_home').Path; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test 'TAOM.Tests/TAOM.Tests.csproj' --filter PartyIconScale --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-24T14:37:06.161169Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.9 seconds
Output:
Failed to add 'C:\Users\mikew\source\repos\TAOM\.codex_dotnet_home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
C:\Program Files\dotnet\sdk\10.0.301\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

Welcome to .NET 10.0!
---------------------
SDK Version: 10.0.301

Telemetry
---------
The .NET tools collect usage data in order to help us improve your experience. It is collected by Microsoft and shared with the community. You can opt-out of telemetry by setting the DOTNET_CLI_TELEMETRY_OPTOUT environment variable to '1' or 'true' using your favorite shell.

Read more about .NET CLI Tools telemetry: https://aka.ms/dotnet-cli-telemetry

----------------
Installed an ASP.NET Core HTTPS development certificate.
To trust the certificate, run 'dotnet dev-certs https --trust'
Learn about HTTPS: https://aka.ms/dotnet-https

----------------
Write your first app: https://aka.ms/dotnet-hello-world
Find out what's new: https://aka.ms/dotnet-whats-new
Explore documentation: https://aka.ms/dotnet-docs
Report issues and find source on GitHub: https://github.com/dotnet/core
Use 'dotnet --help' to see available commands or visit: https://aka.ms/dotnet-cli
--------------------------------------------------------------------------------------

 exited 1 in 920ms:
Failed to add 'C:\Users\mikew\source\repos\TAOM\.codex_dotnet_home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
C:\Program Files\dotnet\sdk\10.0.301\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

Welcome to .NET 10.0!
---------------------
SDK Version: 10.0.301

Telemetry
---------
The .NET tools collect usage data in order to help us improve your experience. It is collected by Microsoft and shared with the community. You can opt-out of telemetry by setting the DOTNET_CLI_TELEMETRY_OPTOUT environment variable to '1' or 'true' using your favorite shell.

Read more about .NET CLI Tools telemetry: https://aka.ms/dotnet-cli-telemetry

----------------
Installed an ASP.NET Core HTTPS development certificate.
To trust the certificate, run 'dotnet dev-certs https --trust'
Learn about HTTPS: https://aka.ms/dotnet-https

----------------
Write your first app: https://aka.ms/dotnet-hello-world
Find out what's new: https://aka.ms/dotnet-whats-new
Explore documentation: https://aka.ms/dotnet-docs
Report issues and find source on GitHub: https://github.com/dotnet/core
Use 'dotnet --help' to see available commands or visit: https://aka.ms/dotnet-cli
--------------------------------------------------------------------------------------

codex
The local dotnet-home retry progressed farther but MSBuild is blocked from reading `C:\Users\mikew\AppData\Local\Microsoft SDKs`, so focused tests cannot run in this sandbox. I created only a temporary `.codex_dotnet_home` directory for that retry and will remove it now.
2026-06-24T14:37:14.198292Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$target = Resolve-Path '"'.codex_dotnet_home'; if ("'$target.Path -like '"'C:\\Users\\mikew\\source\\repos\\TAOM\\*') { Remove-Item -LiteralPath "'$target.Path -Recurse -Force }'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$target = Resolve-Path '"'.codex_dotnet_home'; if ("'$target.Path -like '"'C:\\Users\\mikew\\source\\repos\\TAOM\\*') { Remove-Item -LiteralPath "'$target.Path -Recurse -Force }' in C:\Users\mikew\source\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$target = Resolve-Path '"'.codex_dotnet_home'; if ("'$target.Path -like '"'C:\\Users\\mikew\\source\\repos\\TAOM\\*') { Remove-Item -LiteralPath "'$target.Path -Recurse -Force }'` rejected: blocked by policy
codex
The cleanup command was blocked by the tool policy wrapper before execution. I’ll use an explicit absolute workspace path for the temporary directory so there’s no computed target ambiguity.
2026-06-24T14:37:23.725452Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Remove-Item -LiteralPath 'C:\\Users\\mikew\\source\\repos\\TAOM\\.codex_dotnet_home' -Recurse -Force"` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Remove-Item -LiteralPath 'C:\\Users\\mikew\\source\\repos\\TAOM\\.codex_dotnet_home' -Recurse -Force" in C:\Users\mikew\source\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Remove-Item -LiteralPath 'C:\\Users\\mikew\\source\\repos\\TAOM\\.codex_dotnet_home' -Recurse -Force"` rejected: blocked by policy
codex
Recursive cleanup is blocked by policy even for that explicit workspace path. I’ll leave the temp directory untouched and mention it only if it shows up as relevant; it is not part of the reviewed feature.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path '.codex_dotnet_home' -Force -Recurse -ErrorAction SilentlyContinue | Select-Object -First 20 FullName,Length,Attributes" in C:\Users\mikew\source\repos\TAOM
 succeeded in 528ms:

[32;1mFullName                                                                                                [0m[32;1m Length[0m[32;1m Attribu[0m
[32;1m                                                                                                        [0m[32;1m       [0m[32;1m     tes[0m
[32;1m--------                                                                                                [0m [32;1m------[0m [32;1m-------[0m
C:\Users\mikew\source\repos\TAOM\.codex_dotnet_home\.dotnet                                                     …ectory
C:\Users\mikew\source\repos\TAOM\.codex_dotnet_home\.dotnet\.workloadAdvertisingManifestSentinel10.0.300 0      Archive
C:\Users\mikew\source\repos\TAOM\.codex_dotnet_home\.dotnet\10.0.301.aspNetCertificateSentinel           0      Archive
C:\Users\mikew\source\repos\TAOM\.codex_dotnet_home\.dotnet\10.0.301.dotnetFirstUseSentinel              0      Archive
C:\Users\mikew\source\repos\TAOM\.codex_dotnet_home\.dotnet\10.0.301.toolpath.sentinel                   0      Archive


codex
I’ve finished the read path. The only execution attempt was the focused test command, which is blocked by sandbox/MSBuild SDK access; the static review remains clean.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/PartyIconScale','TAOM.Tests/Features/PartyIconScale' -Recurse -File | Select-String -Pattern 'new List<|new Dictionary<|Enumerable|\\.Select\\(|\\.Where\\(|PartyIconScaleConfig|GetScale|Resolve|TrySwapZeroPointThree' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 388ms:
 M CHANGELOG.md
 M CLAUDE.md
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M docs/reference/banner-icon-generation.md
?? .codex_dotnet_home/
?? Main/Features/NavalTravel/
?? Main/Features/PartyIconScale/
?? Main/_Module/ModuleData/naval_travel/
?? TAOM.Tests/Features/NavalTravel/
?? TAOM.Tests/Features/PartyIconScale/
?? docs/features/naval-travel.md
?? docs/features/party-icon-scale.md
?? docs/reviews/codex-adversarial-PartyIconScale-2026-06-24.md
?? docs/reviews/codex-adversarial-PartyIconScale-2026-06-24.prompt.md
?? docs/reviews/codex-adversarial-navaltravel-2026-06-24.md
?? docs/reviews/codex-adversarial-navaltravel-2026-06-24.prompt.md
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

 succeeded in 530ms:

[32;1mPath                                                                                                [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                                [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                         6 /// Re…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                         9 /// <s…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        12 /// <c…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        19 /// ha…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        23 public…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        38     in…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        44     //…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleConfig.cs                        47     pu…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    12 /// <c…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    20 /// Bo…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    29       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    31       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    33       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    36       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    40       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    44       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    52     //…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    53     pr…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    54       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\PartyIconScaleTranspiler.cs                    63       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                12 /// <c…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                21     pr…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                26       …
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                33     pu…
C:\Users\mikew\source\repos\TAOM\Main\Features\PartyIconScale\Hooks\Patch53_PartyIconScale.cs                34       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs              7 public…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             12     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             14       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             18     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             20       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             21       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             25     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             27       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             28       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             32     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             34       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             35       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             39     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             41       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             45     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             47       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             51     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             53       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             54       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             58     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             60       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             61       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             65     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             67       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             71     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleConfigTests.cs             73       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         15     pr…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         16       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         32     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         34       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         40       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         43       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         48     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         50       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         56       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         59       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         66       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         78       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         80       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         81       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         92       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs         98       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs        105     pu…
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs        107       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs        122       …
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\PartyIconScale\PartyIconScaleTranspilerTests.cs        128       …


codex
**KNOWN SUSPECTS VERDICTS**

S1. REFUTED. [PartyIconScaleTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs:56) only scans the patched method’s instruction stream, and [Patch53_PartyIconScale.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.cs:29) targets only `MobilePartyVisual.AddCharacterToPartyIcon`. Given the supplied v1.4.6 IL, the two matched `0.3f` sites are uniquely distinguished by next instruction: `Scale` for people, `mul` for mount; the third `0.3f` is before `div` and is not matched. `AgentVisualsData.Scale(float)` exists as the expected one-arg method in the decompiled engine source.

S2. REFUTED. Exact `f == 0.3f` at [PartyIconScaleTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs:58) is correct for IL identity matching. `ldc.r4` is a `float32` literal and Harmony exposes it as boxed `System.Single`; no culture path is involved. Epsilon matching would loosen the IL anchor unnecessarily.

S3. REFUTED. The code mutates the existing `CodeInstruction` object at [PartyIconScaleTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs:62), preserving labels/blocks. The label test covers this at [PartyIconScaleTranspilerTests.cs](C:/Users/mikew/source/repos/TAOM/TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs:87). CastleRecruitment uses the same in-place pattern at [CastleAiTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs:75). Stack effect remains one pushed float.

S4. REFUTED. MCM slider bounds are `0.05f..1.0f` at [TaomSettings.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/TaomSettings.cs:569), matching `Min/Max` at [PartyIconScaleConfig.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleConfig.cs:29). `Resolve` uses finite inclusive range validation, and `TaomSettings.Instance == null` falls back to `0.15f` via null propagation at [PartyIconScaleConfig.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleConfig.cs:47).

S5. REFUTED. Missing sites only warn and return the stream; no throw path exists in `Rewrite`. If the transpiler were registered twice, the second pass sees calls instead of `ldc.r4 0.3` and logs warnings. Also, Patch53 is in `OnGameInitializationFinished`, not `OnSubModuleLoad`, and is behind `_gameInitPatchesApplied` at [SubModule.cs](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:563).

S6. REFUTED. Harmony composes transpilers and postfixes normally. Patch53 patches at [SubModule.cs](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:642); BannerColor manually adds the postfix later at [SubModule.cs](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:692). The postfix only reads original args and edits `teamColor1/teamColor2`; Patch53 does not touch the method signature or those parameters.

**FINDINGS**

No P1/P2/P3/NIT findings.

ADR-007: no violation. `GetScale()` reads TAOM-owned `TaomSettings`, not a TaleWorlds sealed type. A service + IoC wrapper would be ceremony here; the static call target is required by the IL site, and `Resolve` is already pure and unit-tested.

Test gaps, non-blocking: no explicit test for duplicate-transpiler idempotency, exception-block preservation, `OpCodes.Call` to `Scale`, or an unrelated earlier `0.3f -> OtherType.Scale` decoy. Under the supplied v1.4.6 IL, none hides a real current bug.

Dead code: source grep found no unused PartyIconScale production declarations.

**THINGS THE CLAUDE DEEP-REVIEW MAY HAVE MISSED**

Patch53 is actually applied in the once-guarded game-init block, not `OnSubModuleLoad`. BannerColor’s same-method postfix is manually patched after category patching, not via its category class. Those details matter for re-apply and ordering analysis.

**QUALITY GATE**

READY TO COMMIT. Static review clean; focused `dotnet test --filter PartyIconScale` could not run because MSBuild was denied access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`.

Note: the blocked dotnet retry created `.codex_dotnet_home/`; cleanup was rejected by the tool policy.
SUCCESS: The process with PID 84180 (child process of PID 73324) has been terminated.
SUCCESS: The process with PID 73324 (child process of PID 76912) has been terminated.
SUCCESS: The process with PID 76912 (child process of PID 85496) has been terminated.
SUCCESS: The process with PID 85496 (child process of PID 21016) has been terminated.
SUCCESS: The process with PID 12504 (child process of PID 45516) has been terminated.
SUCCESS: The process with PID 45516 (child process of PID 35044) has been terminated.
SUCCESS: The process with PID 35044 (child process of PID 81632) has been terminated.
SUCCESS: The process with PID 81632 (child process of PID 19416) has been terminated.
SUCCESS: The process with PID 19416 (child process of PID 21016) has been terminated.
tokens used
114,511
**KNOWN SUSPECTS VERDICTS**

S1. REFUTED. [PartyIconScaleTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs:56) only scans the patched method’s instruction stream, and [Patch53_PartyIconScale.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/Hooks/Patch53_PartyIconScale.cs:29) targets only `MobilePartyVisual.AddCharacterToPartyIcon`. Given the supplied v1.4.6 IL, the two matched `0.3f` sites are uniquely distinguished by next instruction: `Scale` for people, `mul` for mount; the third `0.3f` is before `div` and is not matched. `AgentVisualsData.Scale(float)` exists as the expected one-arg method in the decompiled engine source.

S2. REFUTED. Exact `f == 0.3f` at [PartyIconScaleTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs:58) is correct for IL identity matching. `ldc.r4` is a `float32` literal and Harmony exposes it as boxed `System.Single`; no culture path is involved. Epsilon matching would loosen the IL anchor unnecessarily.

S3. REFUTED. The code mutates the existing `CodeInstruction` object at [PartyIconScaleTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleTranspiler.cs:62), preserving labels/blocks. The label test covers this at [PartyIconScaleTranspilerTests.cs](C:/Users/mikew/source/repos/TAOM/TAOM.Tests/Features/PartyIconScale/PartyIconScaleTranspilerTests.cs:87). CastleRecruitment uses the same in-place pattern at [CastleAiTranspiler.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs:75). Stack effect remains one pushed float.

S4. REFUTED. MCM slider bounds are `0.05f..1.0f` at [TaomSettings.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/TaomSettings.cs:569), matching `Min/Max` at [PartyIconScaleConfig.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleConfig.cs:29). `Resolve` uses finite inclusive range validation, and `TaomSettings.Instance == null` falls back to `0.15f` via null propagation at [PartyIconScaleConfig.cs](C:/Users/mikew/source/repos/TAOM/Main/Features/PartyIconScale/PartyIconScaleConfig.cs:47).

S5. REFUTED. Missing sites only warn and return the stream; no throw path exists in `Rewrite`. If the transpiler were registered twice, the second pass sees calls instead of `ldc.r4 0.3` and logs warnings. Also, Patch53 is in `OnGameInitializationFinished`, not `OnSubModuleLoad`, and is behind `_gameInitPatchesApplied` at [SubModule.cs](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:563).

S6. REFUTED. Harmony composes transpilers and postfixes normally. Patch53 patches at [SubModule.cs](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:642); BannerColor manually adds the postfix later at [SubModule.cs](C:/Users/mikew/source/repos/TAOM/Main/SubModule.cs:692). The postfix only reads original args and edits `teamColor1/teamColor2`; Patch53 does not touch the method signature or those parameters.

**FINDINGS**

No P1/P2/P3/NIT findings.

ADR-007: no violation. `GetScale()` reads TAOM-owned `TaomSettings`, not a TaleWorlds sealed type. A service + IoC wrapper would be ceremony here; the static call target is required by the IL site, and `Resolve` is already pure and unit-tested.

Test gaps, non-blocking: no explicit test for duplicate-transpiler idempotency, exception-block preservation, `OpCodes.Call` to `Scale`, or an unrelated earlier `0.3f -> OtherType.Scale` decoy. Under the supplied v1.4.6 IL, none hides a real current bug.

Dead code: source grep found no unused PartyIconScale production declarations.

**THINGS THE CLAUDE DEEP-REVIEW MAY HAVE MISSED**

Patch53 is actually applied in the once-guarded game-init block, not `OnSubModuleLoad`. BannerColor’s same-method postfix is manually patched after category patching, not via its category class. Those details matter for re-apply and ordering analysis.

**QUALITY GATE**

READY TO COMMIT. Static review clean; focused `dotnet test --filter PartyIconScale` could not run because MSBuild was denied access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`.

Note: the blocked dotnet retry created `.codex_dotnet_home/`; cleanup was rejected by the tool policy.
