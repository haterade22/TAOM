# Code Quality Principles

Guidelines for writing clean, maintainable code.

> **Complement to**: [patterns.md](./patterns.md) which covers design patterns (Strategy, Hook, Adapter). This guide covers **code quality fundamentals** that apply everywhere.

---

## Core Philosophy

> "Any fool can write code that a computer can understand. Good programmers write code that humans can understand." - Martin Fowler

---

## Self-Documenting Code

### Names Tell the Story

Code should read like prose. If you need a comment to explain what code does, the code needs better names.

**Variables:**
```csharp
// Bad
var d = 7;          // days until expiry
// Good
var daysUntilExpiry = 7;

// Bad
var tmp = heroes.Where(h => h.IsAlive);
// Good
var aliveHeroes = heroes.Where(hero => hero.IsAlive);
```

**Methods:**
```csharp
// Bad
void Process(Agent agent)
// Good
void ApplyBonusDamage(Agent attacker, Agent victim)

// Bad
float Calc(float a, float b)
// Good
float CalculateFinalDamageWithModifiers(float baseDamage, float multiplier)
```

**Classes:**
```csharp
// Bad - Too vague
Manager, Handler, Processor
// Good
BonusEngine, PartySpeedCalculator, HeroSkillModifier
```

### Methods Should Do One Thing

A method should:
- Fit on one screen (roughly 20-30 lines max)
- Have one reason to change
- Be testable in isolation

If you're using "and" to describe a method, split it.

### Comments: Why, Not What

**Bad comments** (explain what):
```csharp
// Loop through heroes
foreach (var hero in heroes)
{
    // Check if alive
    if (hero.IsAlive)
    {
        // Add to list
        result.Add(hero);
    }
}
```

**Good comments** (explain why):
```csharp
// Dead heroes retain their equipment but can't be selected for battle (ADR-006)
var selectableHeroes = heroes.Where(h => h.IsAlive);

// TaleWorlds expects null for unset culture, not empty string (game engine quirk)
return culture?.StringId ?? null;
```

---

## Complexity Management

### Avoid Deep Nesting

Maximum nesting depth: 3 levels

```csharp
// Bad - Deep nesting:
if (agent != null)
{
    if (agent.IsActive)
    {
        if (agent.Equipment != null)
        {
            if (agent.Equipment.HasWeapon)
            {
                // actual logic buried here
            }
        }
    }
}

// Good - Guard clauses (early returns):
if (agent is null) return;
if (!agent.IsActive) return;
if (agent.Equipment is null) return;
if (!agent.Equipment.HasWeapon) return;

// actual logic at top level
```

### Guard Clauses

Handle edge cases first, then the main logic:

```csharp
public float CalculateDamageModifier(IAgentAdapter attacker, IAgentAdapter victim)
{
    // Guards first - handle edge cases
    if (attacker is null) return 1.0f;
    if (victim is null) return 1.0f;
    if (!attacker.IsActive) return 1.0f;

    // Main logic - no nesting needed
    var attackerCulture = _cultureService.GetCulture(attacker);
    var victimCulture = _cultureService.GetCulture(victim);
    return _bonusEngine.GetDamageModifier(attackerCulture, victimCulture);
}
```

### Extract Till You Drop

When code gets complex, extract smaller methods:

```csharp
// Bad - One big method:
public void ProcessMissionStart(IMission mission)
{
    // 50 lines of hero initialization
    // 30 lines of equipment setup
    // 40 lines of skill calculation
    // 20 lines of logging
}

// Good - Composed methods:
public void ProcessMissionStart(IMission mission)
{
    InitializeHeroes(mission);
    SetupEquipment(mission);
    CalculateInitialSkills(mission);
    LogMissionDetails(mission);
}
```

---

## Code Organization

### File Structure

One concept per file. If a file has multiple unrelated things, split it.

```
Good structure:
Features/PartySpeed/
+-- PartySpeedEngine.cs         // Single responsibility
+-- PartySpeedService.cs
+-- PartySpeedModifierHook.cs
+-- IPartySpeedEngine.cs

Bad structure:
Features/Misc/
+-- UtilityClasses.cs          // Multiple unrelated classes
```

### Import Organization

Group usings logically:
1. System namespaces
2. Third-party packages (Harmony, DryIoC)
3. TaleWorlds namespaces
4. TAOM namespaces (internal)

```csharp
// System
using System;
using System.Collections.Generic;
using System.Linq;

// Third-party
using HarmonyLib;
using DryIoc;

// TaleWorlds
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

// Internal
using TAOM.Adapters;
using TAOM.Features.PartySpeed;
```

### Consistent Patterns

Within the codebase, similar things should look similar:
- If one service uses adapter interfaces, all should (ADR-007)
- If one hook delegates to a strategy, all should (ADR-002)
- If one test uses MSTest + NSubstitute, all should (ADR-008)

---

## Error Handling

### Be Specific

```csharp
// Bad
catch (Exception) { throw new Exception("Failed"); }
// Good
catch (Exception ex) { throw new HeroInitializationException($"Failed to initialize hero {hero.Name}", ex); }
```

### Fail Fast

Check preconditions early and fail immediately:

```csharp
public void ApplyDamageModifier(ref float damage, IAgentAdapter attacker, IAgentAdapter victim)
{
    ArgumentNullException.ThrowIfNull(attacker);
    ArgumentNullException.ThrowIfNull(victim);

    if (damage < 0)
        throw new ArgumentOutOfRangeException(nameof(damage), "Damage cannot be negative");

    // Now proceed with confidence
    var modifier = CalculateModifier(attacker, victim);
    damage *= modifier;
}
```

### Don't Swallow Errors

```csharp
// Bad
catch (Exception) { /* ignore */ }
// Bad
catch (Exception ex) { _logger.LogError(ex.Message); }  // Log and continue silently

// Good
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to apply bonus for {AttackerName} attacking {VictimName}",
        attacker.Name, victim.Name);
    throw;  // Or handle appropriately
}
```

### Log with Context

Always include relevant context in log messages:

```csharp
// Bad
_logger.LogError("Operation failed");
_logger.LogError(ex.Message);

// Good
_logger.LogError(ex, "Failed to calculate damage modifier. " +
    "Attacker: {AttackerName} (Culture: {AttackerCulture}), " +
    "Victim: {VictimName} (Culture: {VictimCulture})",
    attacker.Name, attackerCulture,
    victim.Name, victimCulture);
```

---

## Magic Numbers and Strings

Extract constants with meaningful names:

```csharp
// Bad
if (hero.Level >= 25)
if (retryCount > 3)
if (cultureId == "empire")

// Good
private const int VeteranHeroLevel = 25;
private const int MaxRetryAttempts = 3;
public static class CultureIds
{
    public const string Empire = "empire";
    public const string Vlandia = "vlandia";
    public const string Battania = "battania";
}

if (hero.Level >= VeteranHeroLevel)
if (retryCount > MaxRetryAttempts)
if (cultureId == CultureIds.Empire)
```

---

## Dependencies

### Depend on Abstractions (ADR-007)

```csharp
// Bad
class BonusService
{
    public BonusService()
    {
        _hero = Hero.MainHero;  // Direct dependency on sealed type
    }
}

// Good
class BonusService
{
    private readonly IHeroAdapter _hero;

    public BonusService(IHeroAdapter hero)  // Abstract dependency
    {
        _hero = hero;
    }
}
```

### Keep Dependencies Explicit

Don't hide dependencies:

```csharp
// Bad
public float CalculateBonus()
{
    var engine = IoC.Resolve<IBonusEngine>();  // Hidden dependency
    return engine.GetBonus();
}

// Good
public float CalculateBonus(IBonusEngine engine)  // Explicit, testable
{
    return engine.GetBonus();
}

// Or use constructor injection (preferred)
public class BonusCalculator
{
    private readonly IBonusEngine _engine;

    public BonusCalculator(IBonusEngine engine)
    {
        _engine = engine;
    }

    public float CalculateBonus() => _engine.GetBonus();
}
```

---

## Code Smells to Watch For

| Smell | Symptom | Fix |
|-------|---------|-----|
| Long Method | > 30 lines | Extract methods |
| Long Parameter List | > 4 parameters | Use parameter object or builder |
| Duplicate Code | Same code in multiple places | Extract shared method |
| Feature Envy | Method uses other class's data more than its own | Move method to that class |
| Data Clumps | Same fields always appear together | Create a class |
| Primitive Obsession | Using strings for IDs, cultures | Create value objects |
| Shotgun Surgery | One change requires many file edits | Consolidate logic |
| God Class | Class does too much | Split responsibilities |

---

## C#-Specific Best Practices

### Nullable Reference Types

```csharp
// Bad - Ignoring nullable warnings:
public string GetHeroName(IHeroAdapter? hero)
{
    return hero.Name;  // CS8602: Possible null reference
}

public void ProcessHero(IHeroAdapter? hero)
{
    var name = hero!.Name;  // Null-forgiving operator hides bug
}

// Good - Explicit null handling:
public string? GetHeroName(IHeroAdapter? hero)
{
    return hero?.Name;  // Caller knows to handle null
}

public string GetHeroNameOrThrow(IHeroAdapter? hero)
{
    return hero?.Name ?? throw new ArgumentNullException(nameof(hero));
}

public void ProcessHero(IHeroAdapter? hero)
{
    if (hero is null)
    {
        _logger.LogWarning("Attempted to process null hero");
        return;
    }

    // Pattern matching has narrowed the type
    Console.WriteLine(hero.Name.ToUpper());
}
```

### LINQ Best Practices

```csharp
// Bad - Multiple enumeration:
public void ProcessHeroes(IEnumerable<IHeroAdapter> heroes)
{
    if (!heroes.Any()) return;              // First enumeration
    var count = heroes.Count();             // Second enumeration
    foreach (var hero in heroes) { ... }    // Third enumeration
}

// Good - Materialize once:
public void ProcessHeroes(IEnumerable<IHeroAdapter> heroes)
{
    var heroList = heroes.ToList();  // Single enumeration

    if (heroList.Count == 0) return;

    foreach (var hero in heroList) { ... }
}

// Bad - Inefficient LINQ chains:
var result = heroes
    .Where(h => h.IsAlive)
    .ToList()                    // Unnecessary materialization
    .Select(h => h.Name)
    .ToList();                   // Another unnecessary materialization

// Good - Efficient LINQ:
var names = heroes
    .Where(h => h.IsAlive)
    .Select(h => h.Name)
    .ToList();  // Single materialization at the end
```

### Dispose Pattern

```csharp
// Bad - Not disposing resources:
public void GenerateReport(string path)
{
    var stream = new FileStream(path, FileMode.Create);
    var writer = new StreamWriter(stream);
    writer.WriteLine("Report");
    // File handle leaked
}

// Good - Using statement:
public void GenerateReport(string path)
{
    using var stream = new FileStream(path, FileMode.Create);
    using var writer = new StreamWriter(stream);
    writer.WriteLine("Report");
    // Both disposed automatically
}
```

---

## Quality Checklist

Before considering code complete:

- [ ] Names are clear and descriptive
- [ ] Methods do one thing
- [ ] No deep nesting (max 3 levels)
- [ ] No magic numbers/strings
- [ ] Errors are handled explicitly with context
- [ ] Dependencies are explicit (constructor injection)
- [ ] Uses adapter interfaces, not sealed types (ADR-007)
- [ ] Tests are passing (TDD!)
- [ ] Code is formatted consistently

---

## Code Quality Gates

### Pre-Completion Checklist

Before marking any code task as complete, verify:

**Type Safety:**
- [ ] No null-forgiving operators (`!`) without documented justification
- [ ] Null handled explicitly at system boundaries
- [ ] Return types are specific (not `object` or `dynamic`)

**Error Handling:**
- [ ] Errors include context (what operation failed, with what inputs)
- [ ] Critical vs non-critical failures are distinguished
- [ ] Resources are properly disposed in all paths

**Code Clarity:**
- [ ] No deeply nested conditionals (max 3 levels)
- [ ] Complex LINQ broken into named steps
- [ ] Magic numbers/strings extracted to named constants
- [ ] Methods do one thing and are testable in isolation

### Proactive Improvement

When encountering code quality issues while modifying code:

1. **Flag the issue** in the response
2. **Propose the fix** with explanation
3. **Ask permission** if the fix is outside the immediate task scope

Example:
> I noticed this method uses a null-forgiving operator that could cause runtime errors:
>
> ```csharp
> var name = hero!.Name;  // Unsafe - no null check
> ```
>
> I recommend explicit null handling:
>
> ```csharp
> if (hero is null)
>     throw new ArgumentNullException(nameof(hero));
> var name = hero.Name;  // Now safe
> ```
>
> Should I include this safety improvement?

---

## Related Guides

- [patterns.md](./patterns.md) - Design patterns (Strategy, Hook, Adapter)
- [tdd-enforcement.md](./tdd-enforcement.md) - Testing principles and TDD workflow
- [architecture.md](./architecture.md) - Overall architecture (Entry Points, Services, Adapters)
- [security.md](./security.md) - Security best practices
- [research-workflow.md](./research-workflow.md) - When you need to understand unfamiliar code
