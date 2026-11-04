# TaleWorlds Source Code Research Guide

**Purpose**: Define when and how AI assistants must research TaleWorlds game mechanics before making implementation decisions.

**Core Principle**: **Never assume or guess about TaleWorlds internals** - always verify through decompiled source code.

---

## Table of Contents
1. [When Research is MANDATORY](#when-research-is-mandatory)
2. [Decompilation Workflow](#decompilation-workflow)
3. [Research Methodology](#research-methodology)
4. [Quick Reference](#quick-reference)

---

## When Research is MANDATORY

AI assistants **MUST** research TaleWorlds decompiled source code before proceeding when:

### 1. Implementing Adapters
- **Before creating any `IXxxAdapter` interface**
  - Verify all properties and methods on the sealed TaleWorlds type
  - Identify read-only properties (get-only) vs read-write
  - Check for computed properties that may have side effects
  - Discover nested sealed types that need recursive wrapping (ADR-007)
- **Example**: Before implementing `IMobilePartyAdapter`, verify `MobileParty` properties in decompiled `TaleWorlds.CampaignSystem.Party.MobileParty`

### 2. Creating Harmony Patches
- **Before defining `[HarmonyPatch]` attributes**
  - Verify exact method signatures (parameters, return types, access modifiers)
  - Check if method is virtual, sealed, or static
  - Identify correct namespace and class hierarchy
  - Confirm method existence across game versions
- **Example**: Before patching `MapScreen.StepSounds`, verify signature in decompiled `SandBox.View.Map.MapScreen`

### 3. Overriding GameModels
- **Before extending TaleWorlds GameModel classes**
  - Verify base class methods and their signatures
  - Check for sealed methods (cannot be overridden)
  - Understand expected return value ranges and constraints
  - Identify what data is available through method parameters
- **Example**: Before overriding `CombatSimulationModel.SimulateHit`, verify signature in decompiled `TaleWorlds.CampaignSystem.ComponentInterfaces.CombatSimulationModel`

### 4. Investigating Bugs
- **Before implementing bug fixes involving game framework**
  - Identify exact vanilla code that's causing the issue
  - Understand game framework expectations and assumptions
  - Verify which properties/methods are null-safe
  - Discover undocumented prerequisites or initialization order

### 5. Making Assumptions About Game Objects
- **Before assuming ANY behavior about**:
  - Property availability or settability
  - Object lifecycle and initialization timing
  - Null safety of game framework objects
  - Side effects of property setters or method calls
  - Required vs optional object properties
- **Example**: Never assume `PartyBase.Culture` is settable - verify through decompiled code (it's read-only!)

### 6. Uncertain About Integration Points
- **When you don't have complete information about**:
  - How TaleWorlds systems call into TAOM
  - What data is available at integration points
  - Event timing and ordering
  - Game framework lifecycle hooks
- **Action**: Research first, ask user if decompiled source is insufficient

---

## Decompilation Workflow

Read the relevant [engine process study](../reference/engine/) first for concepts,
call paths and TAOM-specific failure modes. Then verify the exact signatures and
load-bearing behavior against the installed game. A study or old dump does not
replace that version check. See [development machines](../reference/development-machines.md)
for the actual game and dump roots, and the [Codex guide](codex-operating-guide.md)
for client/tool discovery.

### Targeted source helper

The repository [source helper](../../tools/taom-src.ps1) takes a short or fully
qualified type name and locates its assembly across the selected game bin and module bins:

```powershell
pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Party.MobileParty
```

Read the returned file, and check the selected assembly and game version before
using it as evidence. The helper prefers an available `BANNERLORD_OVERRIDE_DIR`
over `BANNERLORD_GAME_DIR`; inspect its output rather than assuming which install
was selected. It writes a versioned cache under the user's `.taom-src` directory.
That may need permission outside a workspace-only sandbox. Do not automatically
invoke `clean` or `remove`, or broaden permissions after a denied cache write.

### Direct ILSpy or a connected MCP server

For a known installed assembly, use `ilspycmd` directly. This is PowerShell
syntax, not the `%VARIABLE%` expansion used by cmd.exe:

```powershell
if (-not $env:BANNERLORD_GAME_DIR) { throw "Set the actual game root before this lookup." }
$taomAssembly = Join-Path $env:BANNERLORD_GAME_DIR 'bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll'
if (-not (Test-Path -LiteralPath $taomAssembly)) { throw "Assembly not found at the selected game root." }
ilspycmd $taomAssembly -t 'TaleWorlds.CampaignSystem.Party.MobileParty'
```

Some types live in module assemblies, not the top-level bin. Shipping client,
server and editor builds also differ; locate the right assembly before concluding
that a type is absent. A namespace is not necessarily an assembly filename.

A connected ILSpy MCP server is another option. Discover the actual tools and
argument schemas in the current client, then verify a harmless lookup. Neither
`.vscode/mcp.json`, root `.mcp.json` nor `.codex/config.toml` establishes that
another client loaded a server or has access to its paths. Do not copy a tool
call from an old transcript and claim it ran.

### Bulk decompilation and refreshes

Use targeted reads for an ordinary API question. For an explicitly scoped dump
refresh, the repository has [decompile_bannerlord.ps1](../../tools/decompile_bannerlord.ps1)
(`-Out`, `-GameBin`) and [decompile_to_folder.ps1](../../tools/decompile_to_folder.ps1)
(`-Source`, `-Destination`, optional `-Force`). Inspect the selected script,
its assembly selection and overwrite behavior before running it. Pass verified
machine-specific paths; do not rely on desktop defaults on another machine.

Bulk helpers can cover different builds and dependencies. Apply
[provenance and no-decompile restrictions](../../.claude/rules/provenance.md)
before authorizing their scope. Keep proprietary output outside Git and external
review packets. Check actual coverage, skipped directories, errors and version
fingerprints after a refresh; file count alone does not prove a complete or
current dump. Recheck the installed DLL after a game update.

### Native engine code (`TaleWorlds.Native.dll`)

The managed decompile ends where a method calls the engine: an `[EngineMethod("x")]`
member of an engine interface, `MBAPI.IMB*` (MountAndBlade),
`EngineApplicationInterface.I*` (Engine) or `LibraryApplicationInterface.I*`
(DotNet). Animation playback, action and monster-usage tables, physics and native
agent AI routines (`Agent_ai::`) run natively, and data such as action sets is parsed
there too. Do not describe that behaviour from naming; decompile it with
[native_decompile.py](../../tools/native_decompile.py):

- `--engine-method <name>`: what a managed engine call does. It accepts the C# name
  (`IMBAgent.GetCurrentActionType`, namespace-qualified or not), the engine name
  (`get_current_action_type`, `MBAgent.get_current_action_type`), the enum member and
  the `IMono_MBAgent::` assert form. The name resolves through the engine's id
  registration: the shipping build keeps almost none of the names as strings.
- `--string <text>`: the native code using an XML attribute, a source-file path
  (`monster_usage.cpp`) or an assert text, each function decompiled.
- `--rva <offset>`: a known address, such as a crash site.

The output is Ghidra pseudo-C. Treat it as evidence of what that binary does, cite
the RVA with the engine version, and re-derive it after an engine bump: native
addresses move between builds. Setup and limits:
[ghidra-native-decompile.md](../features/ghidra-native-decompile.md).

---

## Research Methodology

### Step-by-Step Research Process

**1. Identify the Knowledge Gap**

Be specific about what you don't know:
- Bad: "I'm not sure how parties work"
- Good: "Does `MobileParty.Culture` have a public setter?"
- Good: "What parameters does `MapScreen.StepSounds` accept?"
- Good: "Is `PartyVisual` returned by `GetVisualOfParty` ever null?"

**2. Locate the Relevant Assembly**

Use the targeted source helper or the actual installed assembly inventory.
Record the resolved DLL, build and version. Do not infer the assembly name from
a namespace or assume a type must be in the top-level game bin. See the
[decompilation workflow](#decompilation-workflow) above.

**3. Search and Decompile**

Use the helper, direct ILSpy or a connected MCP tool as described above. To
navigate an existing dump, first resolve the machine's real dump root:

```powershell
if (-not $env:TAOM_DECOMPILE_ROOT) { throw "Resolve the actual dump path before searching." }
rg -n --glob '*.cs' 'class MobileParty|void StepSounds|CultureObject Culture' $env:TAOM_DECOMPILE_ROOT
```

A search hit is a navigation aid. Verify the relevant getter, method signature,
call site and behavior in the installed assembly before making an engine claim.

**4. Analyze the Code**

Focus on:
- **Properties**: Does it have a setter? Is it computed?
- **Methods**: Exact signature (parameters, return type, access modifiers)
- **Null Safety**: Are null checks present in vanilla code?
- **Side Effects**: Does calling this method/property have side effects?
- **Dependencies**: What other types/properties does this depend on?

**5. Document Your Findings**

Record the findings in the feature doc, or in the commit body when no doc exists

**6. Verify Understanding**

Before proceeding, confirm:
- You understand WHY the code works this way
- You know what data is available/unavailable
- You can explain limitations or constraints
- You've documented discoveries for future reference

---

## Quick Reference

### Research Checklist

Before implementing ANY TaleWorlds-related code, verify:

- [ ] **Properties**: Are they read-only or read-write?
- [ ] **Methods**: What are the exact signatures?
- [ ] **Null Safety**: Can objects/properties be null?
- [ ] **Sealed Types**: What nested types need adapters?
- [ ] **Integration Points**: When does TaleWorlds call this?
- [ ] **Data Availability**: What data is accessible vs unavailable?
- [ ] **Prerequisites**: What must be initialized first?
- [ ] **Side Effects**: Does this method/property have side effects?

### When to Ask User vs Research

**Research First (Don't Ask)**:
- Property signatures and accessibility
- Method parameters and return types
- Whether objects can be null
- Basic game framework behavior
- Integration point timing

**Ask User After Research If**:
- Decompiled source is ambiguous or unclear
- Multiple implementation approaches are valid
- Business logic decision required (not technical)
- Game version differences might exist
- Need clarification on mod goals/priorities

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](./agent-operating-manual.md)
- [docs/ai-includes/agent-teams.md](./agent-teams.md)
- [docs/ai-includes/architecture.md](./architecture.md)
- [docs/ai-includes/codex-operating-guide.md](./codex-operating-guide.md)
- [docs/ai-includes/new-culture-authoring.md](./new-culture-authoring.md)
- [docs/features/elephant/howdah-ship-research-2026-09-18.md](../features/elephant/howdah-ship-research-2026-09-18.md)
- [docs/features/ghidra-native-decompile.md](../features/ghidra-native-decompile.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/scripted-melee-strikes.md](../reference/scripted-melee-strikes.md)

<!-- backlinks-end -->
