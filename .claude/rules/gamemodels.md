---
paths:
  - "Main/Features/**/Models/*.cs"
  - "Main/Features/**/*Model.cs"
---

# GameModel Override Rules

TAOM overrides game models by inheriting the engine's model for the slot. All follow the same pattern.

## Pattern

```csharp
public class TaomPartyMoraleModel : DefaultPartyMoraleModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomPartyMoraleModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override ExplainedNumber GetEffectivePartyMorale(MobileParty party, bool includeDescription = false)
    {
        var result = base.GetEffectivePartyMorale(party, includeDescription);
        _feats.ApplyMoraleFeats(CultureFeatAdapter.FromOrNull(party.Party), ref result);
        return result;
    }
}
```

Live example, with one more service call: `Main/Features/CulturalFeats/Models/TaomPartyMoraleModel.cs`.

## Rules

1. **Research first** — Always decompile `DefaultXxxModel` with `pwsh tools/taom-src.ps1 path <Type>` before overriding. Never guess which base methods to call.
2. **Inherit the engine's concrete model for that slot** (`Default*`, `Sandbox*`, `CustomBattle*`, or `TaomAgentApplyDamageModel`); never `GameModel` directly.
3. **Call `base.Method()`**: unless deliberately replacing behavior, fall through to base for unhandled cases. Never delegate to `MBGameModel.BaseModel` or a held model instance: your overrides and initialized state do not reach it (`TaomBattleBannerBearersModel`).
4. **Thin model class** — The model class is an entry point (<150 lines). **All logic goes in a `Service`.** Line count is a ceiling, not the test. The override body may contain ONLY one of: (a) a single constant expression (e.g. `MaxCharacterTier => 10`), (b) perk/adapter conversion at the boundary plus a direct delegate to the service. A body that contains `if`, `foreach`, `switch`, `yield` branching, or any multi-line computation is a violation — extract to a service even if the model is under 20 lines. "It's only a few lines" is not a carve-out; the rule is binary. Counter-example: `TaomCharacterStatsModel` (one constant) is legal; a 6-line `yield return` chain with a conditional is not.
5. **Adapter boundary** — Convert sealed TaleWorlds params to adapters immediately. Never pass `Hero`, `Settlement`, etc. into the service.
6. **JSON/XML config** — Configurable values live in `Main/_Module/ModuleData/configs/` or feature-specific XML, not hardcoded in the model.
7. **Register in SubModule.cs:** GameModel overrides are registered with `campaignStarter.AddModel<TBase>(new TaomXxxModel(...))` inside `SubModule.OnGameStart` (there is no `CreateGameModels()`); resolution is last-registered-wins, so TAOM's call must follow the engine's. `GameModelOverrideBindingTests` looks for the literal `new TaomXxxModel(` in `SubModule.cs`, so register with the unqualified type name and a `using`, not a namespace-qualified `new`. A feature module may instead declare `GameModelDecl.Of<TSlot, TModel>(target, ...)` in `TaomFeatureModule.GameModels`, which `FeatureModuleHooks` adds after the models `SubModule` adds itself; the binding test accepts either route (`SiegeForcesModule` uses it). Do not declare a Custom Battle model for a slot `RegisterCustomBattleModels` fills by hand: the module step runs after it and the last model added wins (`FeatureModulesTests`).
8. **Tests** — Service logic is fully unit-tested. The model class itself is thin enough to not need direct tests.
9. **Cross-entity propagation.** If an override returns a per-entity value, read the engine consumer first. Does the value get pushed to attached or child entities? Is it recomputed per entity? If either is yes, the override is not isolated: mirror it. An entity mid-transition keeps the capability to finish. Pin the decision in a pure service method with a matrix test. When you rewrite one limb of a faithful port, re-audit that limb's whole behaviour (`rca-navaltravel-2026-06-24.md`).

## Existing overrides

The one catalogue of every override (base, feature, thread notes, Custom Battle wiring) is [gamemodel-registry.md](../../docs/reference/gamemodel-registry.md). Add a row there with a new model; `tools/lint_docs.py` checks it against the code.
