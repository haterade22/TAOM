# Agent 2 lens: Bannerlord API Compatibility

Review these files for Bannerlord API compatibility. Focus on TaleWorlds API usage.

CRITICAL: The decompiled dump at E:\Decompiled_Bannerlord\ can lag an engine bump; the INSTALLED DLLs are authoritative. ALWAYS verify against them, with `pwsh tools/taom-src.ps1 path <Full.Type.Name>` or ilspycmd:
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "Full.Type.Name"
  ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "Full.Type.Name"
NEVER trust the decompiled folder for signature verification.

FILES: the list in your spawn prompt.

FOR EACH FILE that references TaleWorlds APIs:
1. Identify every TaleWorlds class, method, property, or enum used
2. Decompile the relevant TaleWorlds type from the INSTALLED DLL to verify:
   - The method/property EXISTS
   - The SIGNATURE matches (parameter types, return type)
   - The method is not marked internal/private
   - For GameModel overrides: the base class method signature is correct
   - For EVERY overridden engine lifecycle virtual (`OnBehaviorInitialize`, `OnGameLoaded`, `AfterStart`, `OnMissionTick`, `OnSessionLaunched`, ...): open its CALLER and quote the line, then state whether the caller reaches an object registered the way TAOM registers it. `MissionBehavior.OnBehaviorInitialize` never fires for a behavior added from `SubModule.OnMissionBehaviorInitialize` (#606, `Mission.AfterStart` :3827 vs :3831) and `MBSubModuleBase.OnGameLoaded` skips new campaigns (2026-09-12): both shipped past this agent because it verified the signature and not the firing set. A signature match is not a firing guarantee.
   - For Harmony patches: the target method exists with the expected signature
   - For every member reached by NAME (`AccessTools.Method` / `PropertySetter` / `Field`, `GetMethod` / `GetField` / `GetProperty` with a literal, `TypeByName`): it resolves today, AND it has a `[DataRow]` in `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` plus a row in `docs/reference/taleworlds-api-snapshot/reflection-sites.md`. A missing row is a finding: the site degrades silently at the next engine bump (MonsterSize's three sites shipped without, #646)
3. **SHARED-ENGINE-TYPE CHECK (MANDATORY when a generic template/class instantiates ONE engine type for MANY logical config variants — e.g. one IssueBase subclass for N issue configs, one MissionBehavior for N spawns).** The engine's `GetType()`-keyed bookkeeping collapses all variants into a single object. Decompile the engine BASE type + its manager/behavior and grep for EVERY path that branches on the runtime type: `GetType()`, `.GetType() ==`, `is <Type>`, `Dictionary<Type,...>`, type-name cooldown keys. Enumerate ALL of them and confirm the collapsed-to-one-type behavior is acceptable for each — do NOT stop at the first one found. The classic miss (Codex review #61): `IssueBase.CheckPreconditions` has TWO type-keyed gates in one method — a soft spawn-over-representation score AND a HARD accept gate (`IssueQuestCanBeDuplicated`, default false, caps the player at one active quest per type). The review found the soft one and shipped the hard one. For IssueBase the full set is: spawn score + per-settlement zero-out + accept gate + cooldown + despawn. See `.claude/rules/csharp-architecture.md` "One Engine Type for Many Config Variants."

4. **ENGINE CLAIMS IN TEXT OR TOOLING (when the change states engine behaviour without calling it: a doc, a rule, a lens, a tool that mirrors an engine code path).** A wrong claim there becomes a wrong rule every later reviewer trusts. Find each claim, open the engine code it describes in the installed DLLs, and report it VERIFIED (quote the line), WRONG (quote it, and give the corrected sentence plus every file:line that states it) or UNVERIFIED (why). Ask whether the described path actually runs (a validation step behind a skip flag, a lifecycle virtual that never fires for TAOM's registration), not only whether the method exists.

OUTPUT FORMAT:
For each API usage:
- ✅ Verified: [Type.Method] — exists with matching signature
- ❌ INCOMPATIBLE: [Type.Method] — [reason: removed/renamed/signature changed]
- ⚠️ UNVERIFIED: [Type.Method] — could not decompile, needs manual check

Summary: X verified, Y incompatible, Z unverified
