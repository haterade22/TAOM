---
paths:
  - "Main/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM C# Design Patterns

Quick reference for the three core patterns. Full details: `docs/ai-includes/patterns.md`

## 1. Hook Pattern (Harmony → Hook Interface → Service)

```
HarmonyPatch (thin)
    └── IOnXxx hook interface
            └── XxxHook implementation
                    └── IXxxService (business logic)
```

- Harmony patch resolves `IOnXxx` hooks via `IoC.ResolveAll<IOnXxx>()`, iterates, delegates
- Hook implementation builds context, calls service
- Service contains all logic — uses adapters, fully testable

```csharp
// Patch — thin, no logic
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class AgentApplyDamageModel_CalculateDamage_Patch
{
    static void Postfix(ref float __result, Agent attacker, Agent victim)
    {
        foreach (var hook in IoC.ResolveAll<IOnCalculateDamage>())
            hook.OnCalculateDamage(ref __result, attacker, victim);
    }
}
```

## 2. Strategy Pattern

For algorithm families with per-culture or per-faction variants:

```csharp
public interface ICultureStrategy
{
    string CultureId { get; }
    float Calculate(IContextAdapter context);
}
// One class per culture, registered as a collection:
container.RegisterMany<ICultureStrategy>(implementations, Reuse.Singleton);
// Service resolves all and dispatches by CultureId
```

## 3. GameModel Override Pattern

```csharp
public class TaomFooModel : DefaultFooModel
{
    private readonly IFooService _service;
    public TaomFooModel(IFooService service) => _service = service;

    public override float Calculate(SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        return _service.Calculate(adapter) ?? base.Calculate(param);
    }
}
```

See `.claude/rules/gamemodels.md` for full GameModel rules.

## Transpiler Note

TAOM uses manual `List<CodeInstruction>` iteration. Harmony 2.4.2 (Bannerlord 1.3) has an expanded `CodeMatcher` API — evaluate it for new transpilers before defaulting to manual iteration.

## Anti-Patterns

- Business logic in Harmony patches (must delegate)
- Sealed types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.` — see adapters.md)
- Multiple responsibilities in one service (split it)
