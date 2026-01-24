# TaleWorlds Source Code Research Guide

**Purpose**: Define when and how AI assistants must research TaleWorlds game mechanics before making implementation decisions.

**Core Principle**: **Never assume or guess about TaleWorlds internals** - always verify through decompiled source code.

---

## Table of Contents
1. [When Research is MANDATORY](#when-research-is-mandatory)
2. [Decompilation Workflow](#decompilation-workflow)
3. [Research Methodology](#research-methodology)
4. [Documentation Patterns](#documentation-patterns)
5. [Integration Analysis Pattern](#integration-analysis-pattern)
6. [Code Comment Pattern](#code-comment-pattern)
7. [Common Research Scenarios](#common-research-scenarios)
8. [Quick Reference](#quick-reference)

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

### Initial Setup (First Time)

**1. Run Decompilation Script**
```powershell
# From repository root
.\Scripts\Decompile-BannerlordAssemblies.ps1
```

**What This Does**:
- Scans 6 game directories (main game + 5 modules: Native, SandBox, SandBoxCore, CustomBattle, StoryMode)
- Decompiles 75+ TaleWorlds assemblies using `ilspycmd`
- Creates project files (.csproj) for each assembly
- Outputs to `Decompiled/` directory

**2. Verify Decompilation**
```powershell
# Check that assemblies were decompiled
ls Decompiled/
# Should show directories like:
# - TaleWorlds.CampaignSystem/
# - TaleWorlds.Core/
# - SandBox/
# - etc.
```

### Incremental Updates

**When to Re-run**:
- After game updates
- When researching newly-discovered assemblies
- If decompiled code seems outdated

**Force Re-decompilation**:
```powershell
.\Scripts\Decompile-BannerlordAssemblies.ps1 -Force
```

### Documentation References
- **Quick Start**: `Scripts/README-Decompilation.md`
- **Comprehensive Guide**: `docs/decompilation-automation.md`

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

Common assemblies by feature area:

| Feature Area | Assembly | Location |
|--------------|----------|----------|
| Campaign parties, heroes, settlements | `TaleWorlds.CampaignSystem` | `Decompiled/TaleWorlds.CampaignSystem/` |
| Core game types (items, characters) | `TaleWorlds.Core` | `Decompiled/TaleWorlds.Core/` |
| UI and view models | `TaleWorlds.GauntletUI` | `Decompiled/TaleWorlds.GauntletUI/` |
| Campaign map screen | `SandBox` | `Decompiled/SandBox/` |
| Mission/battle logic | `TaleWorlds.MountAndBlade` | `Decompiled/TaleWorlds.MountAndBlade/` |
| AI and automation | `TaleWorlds.CampaignSystem.AI` | `Decompiled/TaleWorlds.CampaignSystem/AI/` |

**3. Search Decompiled Source**

Use IDE search or grep:
```powershell
# Search for class definition
grep -r "class MobileParty" Decompiled/TaleWorlds.CampaignSystem/

# Search for method signature
grep -r "void StepSounds" Decompiled/SandBox/

# Search for property
grep -r "CultureObject Culture" Decompiled/
```

**4. Analyze the Code**

Focus on:
- **Properties**: Does it have a setter? Is it computed?
- **Methods**: Exact signature (parameters, return type, access modifiers)
- **Null Safety**: Are null checks present in vanilla code?
- **Side Effects**: Does calling this method/property have side effects?
- **Dependencies**: What other types/properties does this depend on?

**5. Document Your Findings**

Use appropriate documentation pattern (see [Documentation Patterns](#documentation-patterns))

**6. Verify Understanding**

Before proceeding, confirm:
- You understand WHY the code works this way
- You know what data is available/unavailable
- You can explain limitations or constraints
- You've documented discoveries for future reference

---

## Documentation Patterns

### Pattern 1: Bug Investigation Document

**When to Use**: Investigating crashes, unexpected behavior, or vanilla game bugs

**Template**:

```markdown
# [Bug Title]

**Status**: [Investigated/Fixed/Workaround Applied]
**Date**: YYYY-MM-DD
**Investigator**: [Your identifier]

## Problem Summary

**Exception**:
```
[Exact exception message and stack trace]
```

**Reproduction Steps**:
1. [Step 1]
2. [Step 2]
3. [Crash occurs]

**Root Cause**: [Brief explanation]

---

## Vanilla Code Analysis

**Decompiled Source** (`Namespace.Class.Method`):

```csharp
// File: Decompiled/AssemblyName/Path/To/File.cs
// Lines: XXX-YYY

[Paste relevant decompiled code with annotations]

private void MethodName(ParamType param)
{
    SomeType obj = GetSomething();
    if (obj.Property != null)  // CRASH: obj can be null here!
    {
        // ... process
    }
}
```

**Bug Explanation**:
- [What's wrong with the code]
- [Why it crashes/misbehaves]
- [When this occurs (scenarios)]

**Why [Object] Can Be [State]**:
[Explanation based on decompiled understanding of how TaleWorlds creates/manages these objects]

---

## Solution Implemented

### Approach: [Brief description]

**Implementation**:
```csharp
[Your fix code]
```

**Rationale**:
- [Why this approach works]
- [What decompiled code revealed that informed this solution]
- [Any limitations or trade-offs]

---

## Technical Background

[Additional context from decompiled source that helps understand the problem domain]

### Key Discoveries
1. **[Discovery 1]**: [Explanation with decompiled code reference]
2. **[Discovery 2]**: [Explanation with decompiled code reference]

---

## Testing

**Manual Validation Required**: [Yes/No]
- [Why integration tests won't work]
- [What to test in-game]

**Test Scenarios**:
1. [Scenario 1]
2. [Scenario 2]

---

## References

- Decompiled Source: `Decompiled/AssemblyName/Path/To/File.cs:LineNumber`
- Related Code: `Main/Path/To/YourFix.cs:LineNumber`
- Related Docs: [Links to relevant docs]
```

---

### Pattern 2: Integration Analysis Document

**When to Use**: Understanding how TaleWorlds systems call into TAOM, mapping base classes to overrides

**Template**:

```markdown
# TaleWorlds [System Name] Integration Analysis

**Purpose**: Document how TaleWorlds' base [system] works and where TAOM hooks into it

**Date**: YYYY-MM-DD
**Decompiled Assemblies**: [List of assemblies researched]

---

## TaleWorlds Base Classes

**Location**: `Decompiled/TaleWorlds.AssemblyName/Path/To/BaseClass.cs`

**Base Class**: `Namespace.BaseClassName`

**Key Methods**:

### 1. `MethodName(ParamType param1, ParamType2 param2, ...) -> ReturnType`

**Decompiled Signature**:
```csharp
public virtual ReturnType MethodName(ParamType param1, ParamType2 param2, ...)
{
    // [Paste relevant vanilla implementation]
}
```

**Purpose**: [What this method does]
**Called By**: [What TaleWorlds system calls this]
**When Called**: [Timing/trigger conditions]
**Parameters Available**:
- `param1`: [What data this provides]
- `param2`: [What data this provides]

**Return Value Constraints**: [Expected ranges, special values, etc.]

### 2. [Additional methods...]

---

## TAOM Integration Points

**TAOM Override**: `Main/Path/To/YourClass.cs`

**Class**: `YourNamespace.YourClassName : BaseClassName`

**Overridden Methods**:
1. `MethodName` - [What TAOM customizes]
2. [Additional methods]

---

## Call Flow Diagram

```
TaleWorlds System Entry Point
    |
TaleWorlds.Framework.SomeManager.DoSomething()
    |
[Calls virtual method on registered model/component]
    |
TAOM.YourClass.MethodName()  <- TAOM OVERRIDE
    |
[Your custom logic]
    |
base.MethodName() (optional - call vanilla if needed)
```

---

## Data Availability Constraints

### What TAOM CAN Access
- [List of available data through method parameters]
- [Accessible properties on parameters]
- [Additional context available through services]

### What TAOM CANNOT Access
- [Data not provided by TaleWorlds at this integration point]
- [Properties that are internal/private]
- [Context that's not yet initialized]

**Implication**: [How this affects TAOM implementation]

---

## Key Discoveries from Decompiled Source

1. **[Discovery 1]**
   - **Finding**: [What you found]
   - **Source**: `Decompiled/Path/To/File.cs:LineNumber`
   - **Impact**: [How this affects TAOM]

2. **[Discovery 2]**
   - **Finding**: [What you found]
   - **Source**: `Decompiled/Path/To/File.cs:LineNumber`
   - **Impact**: [How this affects TAOM]

---

## Testing Implications

**Testable via Adapters**: [Yes/Partial/No]
- [What can be unit tested]
- [What requires in-game validation]

**Mock Requirements**:
- [What needs to be mocked for testing]
- [Adapter interfaces needed]

---

## References

- TaleWorlds Base: `Decompiled/Assembly/Path.cs:Line`
- TAOM Override: `Main/Path/YourClass.cs:Line`
- Related Adapters: [List relevant adapter interfaces]
```

---

## Code Comment Pattern

**When to Use**: Explaining implementation choices based on decompiled findings directly in code

**Template**:

```csharp
// [Brief explanation of what this code does]
// Based on decompiled TaleWorlds.Assembly.Class.Method:
//   - [Key finding 1 that informs this implementation]
//   - [Key finding 2]
// Source: Decompiled/Assembly/Path/File.cs:LineNumber
[Your code]
```

**Examples**:

**Example 1** (Pattern from investigation docs):
```csharp
// PartyBase.Culture is read-only (computed from MapFaction.Culture)
// From decompiled TaleWorlds.CampaignSystem.Party.PartyBase:
//   public CultureObject Culture { get { return this.MapFaction.Culture; } }
// Therefore, we cannot set it directly - must set via MapFaction
// Source: Decompiled/TaleWorlds.CampaignSystem/Party/PartyBase.cs:142
```

**Example 2** (Null safety):
```csharp
// Vanilla MapScreen.StepSounds crashes if PartyVisual is null
// From decompiled SandBox.View.Map.MapScreen.StepSounds:
//   Does NOT check if GetVisualOfParty returns null before accessing .HumanAgentVisuals
// Source: Decompiled/SandBox/View/Map/MapScreen.cs:873-881
// Therefore, we add defensive null check:
if (visualOfParty != null && visualOfParty.HumanAgentVisuals != null)
{
    // ... safe to proceed
}
```

---

## Integration Analysis Pattern

**Purpose**: Map TaleWorlds base systems to TAOM integration points

**When to Use**:
- Before overriding GameModel classes
- Before implementing new campaign behaviors
- When understanding event timing and data availability

**Key Questions to Answer**:
1. What TaleWorlds base class are we extending?
2. What methods are we overriding?
3. When does TaleWorlds call these methods?
4. What data is available through parameters?
5. What data is NOT available (constraints)?
6. What are the expected return value ranges?

**Process**:
1. Identify the TaleWorlds base class (e.g., `CombatSimulationModel`)
2. Decompile and analyze virtual methods
3. Document call flow from TaleWorlds framework -> TAOM override
4. Map data availability constraints
5. Create integration analysis document using Pattern 2 template

---

## Common Research Scenarios

### Scenario 1: "Does Property X Have a Setter?"

**Research Steps**:
1. Locate the class in decompiled source
2. Find the property definition
3. Check for `set` accessor

**Example - `PartyBase.Culture`**:

**Decompiled Source** (`Decompiled/TaleWorlds.CampaignSystem/Party/PartyBase.cs:142`):
```csharp
public CultureObject Culture
{
    get { return this.MapFaction.Culture; }
    // No setter - READ-ONLY!
}
```

**Conclusion**: `PartyBase.Culture` is read-only (computed property). Cannot be set directly.

**Documentation**: Add code comment or investigation doc explaining this constraint

---

### Scenario 2: "What Are the Exact Method Parameters?"

**Research Steps**:
1. Locate the method in decompiled source
2. Copy exact signature (parameter types, names, order, modifiers)
3. Note access modifiers (public/private/protected/internal)
4. Check if method is virtual, sealed, or static

**Example - `MapScreen.StepSounds`**:

**Decompiled Source** (`Decompiled/SandBox/View/Map/MapScreen.cs:873`):
```csharp
private void StepSounds(MobileParty party)
{
    // ... implementation
}
```

**Conclusion**: Method signature is `private void StepSounds(MobileParty party)`

**Documentation**: Use exact signature in `[HarmonyPatch]` attribute

---

### Scenario 3: "Can Object X Ever Be Null?"

**Research Steps**:
1. Find where TaleWorlds creates/returns the object
2. Look for null checks in vanilla code
3. Identify scenarios where null might occur
4. Check if vanilla code is defensive about null

**Example - `PartyVisualManager.Current.GetVisualOfParty()`**:

**Decompiled Source** (`Decompiled/SandBox/View/Map/MapScreen.cs:875-881`):
```csharp
private void StepSounds(MobileParty party)
{
    if (party.IsVisible && party.MemberRoster.TotalManCount > 0)
    {
        PartyVisual visualOfParty = PartyVisualManager.Current.GetVisualOfParty(party.Party);
        if (visualOfParty.HumanAgentVisuals != null)  // No null check on visualOfParty!
        {
            // ... process
        }
    }
}
```

**Conclusion**: `GetVisualOfParty` CAN return null, but vanilla code doesn't check. This is a vanilla bug.

**Documentation**: Create bug investigation document (Pattern 1) and add defensive code

---

### Scenario 4: "What Properties Must Be Set on Game Objects?"

**Research Steps**:
1. Search for crashes mentioning missing properties
2. Decompile vanilla code that creates similar objects
3. Look for property assignments in vanilla initialization
4. Check for validation code that expects properties

**Example - Custom Battle Character Properties**:

**Decompiled Source** (various files in `TaleWorlds.Core/`):
```csharp
// Vanilla character creation always sets:
character.Culture = someCulture;         // REQUIRED
character.Occupation = someOccupation;   // REQUIRED
character.IsFemale = false;              // REQUIRED
character.Race = someRace;               // REQUIRED (custom battles)
```

**Conclusion**: These properties are essential and must be set

**Documentation**: See `docs/ai-includes/patterns.md` for essential property pattern

---

### Scenario 5: "Does This Property Access Nested Objects Internally?"

**CRITICAL for Adapters** - Determines whether to use null-conditional operators (`?.`)

**Research Steps**:
1. Locate the property in decompiled source
2. Examine the property getter implementation (not just the signature!)
3. Check if getter accesses other properties/fields before returning value
4. Identify any intermediate objects that could be null
5. Verify if TaleWorlds has null checks in the getter

**Example - `PartyBase.Culture`**:

**Decompiled Source** (`Decompiled/TaleWorlds.CampaignSystem/Party/PartyBase.cs`):
```csharp
public class PartyBase
{
    public IFaction MapFaction { get; }  // Can be null!

    // Computed property accessing nested object WITHOUT null check
    public CultureObject Culture
    {
        get { return this.MapFaction.Culture; }  // Crashes if MapFaction is null!
    }
}
```

**Problem**: Accessing `party.Culture` when `MapFaction` is null throws `NullReferenceException` **BEFORE** adapter's null check executes.

**WRONG Adapter Implementation**:
```csharp
// This CRASHES when MapFaction is null!
public ICultureAdapter Culture => _partyBase.Culture != null
    ? _factory.GetCultureAdapter(_partyBase.Culture)
    : null;
```

**CORRECT Adapter Implementation**:
```csharp
// Safe - checks MapFaction first with null-conditional operator
public ICultureAdapter Culture => _partyBase.MapFaction?.Culture != null
    ? _factory.GetCultureAdapter(_partyBase.MapFaction.Culture)
    : null;
```

**When This Occurs**:
- Bandit parties (no faction affiliation)
- Hideout parties (no MapFaction)
- Parties during creation (faction not yet assigned)

**Conclusion**: Computed properties that access nested objects require null-conditional operators on intermediate objects.

**Documentation**:
- Add null-conditional operators in adapter implementation
- Reference [ADR-007 Null-Conditional Operators section](../adrs/007-adapter-pattern.md#null-conditional-operators-for-computed-properties-critical)
- Document in code comments why `?.` is needed
- Create tests for null scenarios

---

### Scenario 6: "How Does TaleWorlds System X Work?"

**Research Steps**:
1. Identify entry point (where does TaleWorlds start the process?)
2. Follow call chain through decompiled source
3. Map data flow and transformations
4. Identify integration points where TAOM can hook in
5. Create integration analysis document (Pattern 2)

**Example - Auto-Resolve Battle System**:

**Entry Point**: `TaleWorlds.CampaignSystem.MapEvents.MapEvent.ApplySimulatedBattleResults()`

**Call Chain**:
```
MapEvent.ApplySimulatedBattleResults()
    |
Campaign.Current.Models.CombatSimulationModel.SimulateBattle()  <- TAOM CAN OVERRIDE
    |
CombatSimulationModel.SimulateHit()  <- TAOM CAN OVERRIDE
```

**Documentation**: Create integration analysis using Pattern 2

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

### Common Decompilation Locations

| What You're Looking For | Likely Location |
|-------------------------|-----------------|
| Party/Hero/Settlement classes | `Decompiled/TaleWorlds.CampaignSystem/` |
| Item/Character/Equipment | `Decompiled/TaleWorlds.Core/` |
| Campaign behaviors | `Decompiled/TaleWorlds.CampaignSystem/CampaignBehaviors/` |
| GameModels | `Decompiled/TaleWorlds.CampaignSystem/GameComponents/` |
| UI ViewModels | `Decompiled/TaleWorlds.GauntletUI/` or `Decompiled/SandBox/GauntletUI/` |
| Map screen | `Decompiled/SandBox/View/Map/` |
| Mission/Battle | `Decompiled/TaleWorlds.MountAndBlade/` |

### Documentation Quick Links

- **Decompilation Setup**: `Scripts/README-Decompilation.md`
- **Decompilation Details**: `docs/decompilation-automation.md`
- **Essential Properties Pattern**: `docs/ai-includes/patterns.md`
- **Adapter Pattern (ADR-007)**: `docs/adrs/007-adapter-pattern.md`

---

## Summary

**Core Principle**: Never assume or guess about TaleWorlds internals - always verify through decompiled source code.

**Workflow**:
1. Identify knowledge gap
2. Run decompilation if needed
3. Search decompiled source
4. Analyze and verify understanding
5. Document findings
6. Proceed with verified implementation

**Remember**: The decompiled source code is your source of truth for TaleWorlds game mechanics. Use it proactively to avoid bugs, crashes, and incorrect assumptions.
