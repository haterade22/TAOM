# Harmony fork feasibility

> Status: not pursued. TAOM bundles the `Lib.Harmony` NuGet package through `Dependencies/TAOM.Dependencies.csproj` (the DR3 change of 2026-05-22; see [dr3-maintenance.md](../migration/dr3-maintenance.md)). This note records the question that was researched before that choice, so nobody repeats it. The findings below come from that research session and were not re-verified when this note was written.

## The question

Could TAOM fork Harmony 2.4.2 into its own dependencies module, renamed `TAOM.Harmony`, and drop the external BUTR Harmony module?

## What the research found

- **Shared patch state survives a rename.** `HarmonySharedState` is a type that Harmony finds with `Type.GetType("HarmonySharedState")`, a name search that is not assembly-qualified, and it stores patches as serialized `byte[]` data. A renamed assembly would therefore still share patch state with other mods' Harmony copies. [dr3-maintenance.md](../migration/dr3-maintenance.md) names the same cross-assembly state as the reason ILRepack consolidation of the bundled DLLs is risky.
- **What the fat `0Harmony.dll` holds.** Harmony core plus MonoMod.Core, MonoMod.Utils, Mono.Cecil and Iced.Intel, merged by ILRepack, about 48K lines decompiled (1,392 files, roughly 90 of them Harmony core). All five are MIT-licensed. On Windows the whole thing is managed code.
- **Format constraint.** The patch-info serialization is a binary format at version 102, and a fork has to leave it unchanged.
- **Plan if it were done.** Decompile the fat DLL, rename the assembly, keep only the `net472` target, add it beside the UIExtenderEx fork, remove `Bannerlord.Harmony` from the dependency list, and replicate the three safety checks in Bannerlord.Harmony's SubModule (an `UnpatchAll()` guard, a version check and a load-order check).

## Risks that were listed

- Decompiler artifacts (compiler-generated names such as `<>c__DisplayClass` and `<Module>`) need cleanup by hand.
- MonoMod.Core's CLR runtime shim reads `MethodDesc` structures through unsafe pointer arithmetic, which breaks if the CLR version changes.
- Harmony ships four to eight releases a year, so every upstream merge is recurring work.
- The Iced.Intel disassembler is about 600 files; it rarely changes but is a large surface.

## Why it matters now

Bumping the bundled package is a version edit plus the lockstep rules in [dr3-maintenance.md](../migration/dr3-maintenance.md), checked by `BundledDependencyManifestTests`. Reopen this question only if a Harmony release breaks Bannerlord and upstream will not fix it.
