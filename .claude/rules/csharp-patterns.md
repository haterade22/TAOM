---
paths:
  - "Main/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM C# Design Patterns

Quick reference for the three core patterns. Full details: `docs/ai-includes/patterns.md`

## 1. Hook Pattern (optional: Harmony → Hook Interface → Service)

Optional layer. Most patches resolve their service at the boundary and call it directly (patch → service → adapter, CLAUDE.md "Architecture (One-liner)"). Add an `IOnXxx` hook only when the patch needs a narrow seam over a wide service (`PartyUpgradeResourceCheckHook` narrows `ISpecialResourceService` for the party-upgrade patches) or a test fakes the hook; a hook that forwards one call adds a file and hides nothing.

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

## Conversation tags

A `ConversationTag` subclass anywhere in TAOM.dll must be concrete, non-generic, public-parameterless and carry a unique `StringId`: the `ConversationManager` constructor instantiates every one at each campaign start and load (v1.4.8 `ConversationManager.cs:239`, `Activator.CreateInstance` then `Dictionary.Add` on the id), so one bad tag breaks every campaign. No shared base class. The gate is `EveryTaomConversationTag_IsInstantiableAndUnique` (`TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`).

## Anti-Patterns

- Business logic in Harmony patches (must delegate)
- Sealed types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.` — see adapters.md)
- Multiple responsibilities in one service (split it)
- A local fear effect that drains morale below 0.01 with nothing said: each rout spreads a morale loss to up to 10 allies, from the whole formation when fewer stand within 4 m. Floor the morale, or say in the feature doc that its routs spread ([lesson](../../docs/reviews/lessons/adapters-taleworlds-api.md))
