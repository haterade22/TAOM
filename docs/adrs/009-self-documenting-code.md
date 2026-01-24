# ADR-009: Self-Documenting Code Standards

**Status**: Accepted
**Date**: 2025-11-23
**Context**: Code documentation and readability standards

## Decision

Code must be self-documenting through clear naming and design. Comments are minimized and kept ELI5-simple.

### Rules

1. **No Comments Inside Methods**: Method bodies must be readable without comments. If code needs explanation, it requires immediate refactoring.
2. **Minimal Method/Class Descriptions**: Summaries must be as short as possible. Use ELI5 (Explain Like I'm Five) principle.
3. **Self-Evident Code**: Names and structure explain intent. Code reads like English.

## Rationale

### Problems with Over-Commenting

1. **Comments Rot**: Code changes, comments don't, creating misleading documentation
2. **Crutch for Bad Design**: Comments compensate for poor naming and structure instead of fixing the root cause
3. **Noise**: Verbose documentation obscures the actual code
4. **Maintenance Burden**: More comments = more maintenance = more drift from reality
5. **False Security**: Developers trust outdated comments instead of reading code

### Benefits of Self-Documenting Code

- **Single Source of Truth**: Code itself is the documentation
- **Forces Good Design**: If code needs explanation, refactor until it doesn't
- **Always Current**: Compiler enforces correctness, no drift
- **Faster Reading**: Less text = faster comprehension
- **Clearer Intent**: Good names reveal purpose better than paragraphs

## Examples

### BAD: Comments Compensating for Poor Code

```csharp
public class Service
{
    /// <summary>
    /// This method calculates the total damage dealt by a unit during a battle engagement.
    /// It takes into account the unit's weapon effectiveness, armor penetration capabilities,
    /// the target's defensive statistics including armor rating and shield effectiveness,
    /// and applies various modifiers based on terrain, weather conditions, and unit morale.
    /// The calculation follows the game's combat formula as specified in the design document.
    /// </summary>
    /// <param name="attacker">The attacking military unit object</param>
    /// <param name="defender">The defending military unit object</param>
    /// <param name="context">Combat context containing environmental factors</param>
    /// <returns>The final calculated damage value as a floating point number</returns>
    public float CalcDmg(IUnitAdapter attacker, IUnitAdapter defender, CombatContext context)
    {
        // First get the base damage from the weapon
        var bd = attacker.Weapon.Damage;

        // Then apply the armor modifier
        var am = defender.Armor.Rating * 0.5f; // Divide by 2 for armor effect

        // Calculate the terrain penalty or bonus
        var tm = context.Terrain == TerrainType.Hills ? 1.2f : 1.0f; // Hills give 20% bonus

        // Don't forget to multiply everything together
        return bd * am * tm;
    }
}
```

### GOOD: Self-Documenting with Minimal Description

```csharp
/// <summary>
/// Calculates battle damage with weapon, armor, and terrain modifiers.
/// </summary>
public class CombatDamageCalculator
{
    public float Calculate(IUnitAdapter attacker, IUnitAdapter defender, CombatContext context)
    {
        var baseDamage = attacker.GetWeaponDamage();
        var armorReduction = defender.GetArmorReduction();
        var terrainModifier = GetTerrainModifier(context.Terrain);

        return baseDamage * armorReduction * terrainModifier;
    }

    private float GetTerrainModifier(TerrainType terrain) =>
        terrain == TerrainType.Hills ? 1.2f : 1.0f;
}
```

### BAD: Inline Comments Explaining Logic

```csharp
public void ProcessBattle(IBattleAdapter battle)
{
    // Loop through all units in the battle
    foreach (var unit in battle.Units)
    {
        // Check if the unit is still alive before processing
        if (unit.IsAlive)
        {
            // Get the current health percentage
            var healthPercent = unit.Health / unit.MaxHealth;

            // If health is below 30%, unit should retreat
            if (healthPercent < 0.3f)
            {
                // Set retreat flag to true
                unit.SetRetreating(true);

                // Log the retreat decision for debugging
                _logger.Info($"Unit {unit.Id} retreating at {healthPercent * 100}% health");
            }
        }
    }
}
```

### GOOD: Refactored to Be Self-Explanatory

```csharp
/// <summary>
/// Orders critically wounded units to retreat.
/// </summary>
public void ProcessBattle(IBattleAdapter battle)
{
    var aliveUnits = battle.Units.Where(u => u.IsAlive);

    foreach (var unit in aliveUnits)
    {
        if (IsCriticallyWounded(unit))
        {
            OrderRetreat(unit);
        }
    }
}

private bool IsCriticallyWounded(IUnitAdapter unit) =>
    unit.GetHealthPercentage() < 0.3f;

private void OrderRetreat(IUnitAdapter unit)
{
    unit.SetRetreating(true);
    _logger.Info($"Unit {unit.Id} retreating at {unit.GetHealthPercentage():P0} health");
}
```

### BAD: Verbose Class Documentation

```csharp
/// <summary>
/// The FormationTargetingService is responsible for analyzing enemy formations during combat
/// and determining which specific formation presents the most valuable target for allied forces
/// to engage. This service takes into consideration multiple factors including formation strength,
/// positioning on the battlefield relative to friendly units, formation type and composition,
/// current morale levels, equipment quality, and strategic importance to the overall battle outcome.
///
/// The service implements a sophisticated scoring algorithm that weighs these various factors
/// and produces a prioritized list of target formations. It is designed to be called by the
/// tactical AI system during the decision-making phase of combat, typically at the start of
/// each combat round or when significant battlefield conditions change.
///
/// Usage: Inject this service into your tactical AI controller and call SelectPriorityTarget()
/// to get the optimal formation to engage.
/// </summary>
public class FormationTargetingService
```

### GOOD: Concise ELI5 Documentation

```csharp
/// <summary>
/// Picks which enemy formation to attack based on threat and opportunity.
/// </summary>
public class FormationTargetingService
```

### BAD: Magic Numbers with Comments

```csharp
public float CalculateSpeed(IPartyAdapter party)
{
    var baseSpeed = party.BaseSpeed;

    // Apply 15% speed bonus for cavalry
    if (party.HasCavalry)
        baseSpeed *= 1.15f;

    // Reduce speed by 25% in forests
    if (party.Terrain == TerrainType.Forest)
        baseSpeed *= 0.75f;

    return baseSpeed;
}
```

### GOOD: Named Constants Instead of Comments

```csharp
/// <summary>
/// Calculates party speed with cavalry and terrain modifiers.
/// </summary>
public class PartySpeedCalculator
{
    private const float CavalrySpeedBonus = 1.15f;
    private const float ForestSpeedPenalty = 0.75f;

    public float Calculate(IPartyAdapter party)
    {
        var speed = party.BaseSpeed;

        if (party.HasCavalry)
            speed *= CavalrySpeedBonus;

        if (party.IsInForest())
            speed *= ForestSpeedPenalty;

        return speed;
    }
}
```

## What Comments ARE Acceptable

### 1. Public API Summaries (ELI5 Only)

```csharp
/// <summary>
/// Finds shortest path between settlements.
/// </summary>
public interface IPathfindingService
{
    /// <summary>
    /// Returns ordered list of settlements from start to end.
    /// </summary>
    List<ISettlementAdapter> FindPath(ISettlementAdapter start, ISettlementAdapter end);
}
```

### 2. Non-Obvious Algorithms (Rare, with External References)

```csharp
/// <summary>
/// A* pathfinding. See: https://en.wikipedia.org/wiki/A*_search_algorithm
/// </summary>
public class AStarPathfinder
```

### 3. "Why" Not "What" (Only When Truly Non-Obvious)

```csharp
// TaleWorlds bug: settlement food model null during loading screens
// Workaround: cache last known value and return it during load
if (Settlement.FoodModel == null)
    return _foodCache.GetLastValue(settlement);
```

## Enforcement

### Mandatory Refactoring Triggers

If you find yourself writing ANY of these, **STOP** and refactor:

1. **Comment explaining what code does** -> Extract method with descriptive name
2. **Comment explaining variable purpose** -> Rename variable
3. **Comment explaining condition** -> Extract to well-named method
4. **Comment explaining algorithm** -> Extract to class with descriptive name
5. **Multi-paragraph XML summary** -> Simplify to one sentence

### Code Review Checklist

- [ ] No comments inside method bodies (except rare workarounds)
- [ ] All XML summaries <= 1 sentence (ELI5)
- [ ] Method names explain what they do
- [ ] Variable names explain what they hold
- [ ] Logic reads like English
- [ ] Magic numbers replaced with named constants

## Migration Strategy

When encountering over-commented code:

1. **Read Comment**: Understand what it's explaining
2. **Extract Method**: Create method with name from comment content
3. **Delete Comment**: Remove now-redundant comment
4. **Verify Tests**: Ensure behavior unchanged
5. **Simplify XML**: Reduce any verbose XML summaries to ELI5

### Example Migration

```csharp
// BEFORE
public void Process(IPartyAdapter party)
{
    // Calculate food consumption per day based on party size
    var foodConsumption = party.MemberCount * 2.5f;

    // Reduce food stores by consumption amount
    party.Food -= foodConsumption;
}

// AFTER
public void Process(IPartyAdapter party)
{
    var dailyConsumption = CalculateDailyFoodConsumption(party);
    party.ConsumeFood(dailyConsumption);
}

private float CalculateDailyFoodConsumption(IPartyAdapter party) =>
    party.MemberCount * FoodPerPersonPerDay;
```

## Consequences

### Positive
- Code is always accurate (compiler-enforced)
- Forces better design and naming
- Faster reading and comprehension
- Eliminates stale documentation
- Reduces maintenance burden

### Negative
- Developers accustomed to verbose comments must adapt
- Initial refactoring effort for complex legacy code
- Some domain knowledge may need external documentation

## Related ADRs

- [ADR-002](./002-thin-entry-points.md): Thin entry points force simple, readable code
- [ADR-003](./003-no-regions.md): Proper decomposition over organizational markers
