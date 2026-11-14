# Bannerlord GameModel system — how overrides plug in (Phase 7)

> **One process, traced from the decompile** (v1.4.5): how `GameModel`s are registered + resolved, and the
> override mechanism behind TAOM's `TaomXxxModel : DefaultXxxModel` overrides (the table in
> [gamemodel-registry.md](../gamemodel-registry.md), 70 on 2026-11-03, which `tools/lint_docs.py` checks against the code).
> This is the mod's single biggest extension surface. Part of the phased engine study.

## WHAT it is

A `GameModel` is a swappable bundle of game-rule calculations (wages, party speed, loyalty, damage, combat sim,
recruitment, etc.). The engine never hard-codes these formulas — it calls `…Models.<SomeModel>.<Method>(...)`. A mod
**overrides** a model by subclassing the vanilla `DefaultXxxModel`, overriding the methods it wants, and registering
the subclass so it wins. This is the sanctioned, no-Harmony way to change game rules.

## HOW it works

### Registration + resolution (`GameModelsManager`, MountAndBlade.cs:61602)
Models live in an **ordered list** `_models`. Two registration methods:
```
public void AddModel(GameModel gameModel)              // :61614 — just _models.Add(gameModel)
public void AddModel<T>(MBGameModel<T> gameModel)      // :61619 — gameModel.Initialize(GetModel<T>()); _models.Add(…)
public T GetModel<T>() where T : GameModel             // :61602
{ for (i = _models.Count-1; i >= 0; i--) if (_models[i] is T r) return r;  return null; }   // ← LAST-added wins ⭐
```
**`GetModel<T>()` scans the list BACKWARD and returns the first `is T` match — so the *last-added* model of a type
wins.** Because `TaomFooModel : DefaultFooModel : FooModel`, a Taom model added *after* the vanilla `DefaultFooModel`
is the one `GetModel<DefaultFooModel>()` / `GetModel<FooModel>()` returns. The Default stays in the list but is
**shadowed**, not removed.

### The two override styles
- **Inheritance (TAOM's way):** `AddModel(new TaomFooModel(...))` where `TaomFooModel : DefaultFooModel` overrides
  specific methods and calls `base.Foo(...)` for everything else. Simple, no decorator. (This is the GameModel
  Override Pattern in `.claude/rules/csharp-patterns.md` / `gamemodels.md`.)
- **Decorator/wrapper (`AddModel<T>` + `MBGameModel<T>`):** `gameModel.Initialize(GetModel<T>())` hands the *previous*
  model to the new one so it can delegate. **ADOD_Beasts used this** (its `ADODAgentStatCalculateModel` wraps the previous
  model); TAOM prefers inheritance + `base` (simpler — see the [ADOD_Beasts comparison](../adod-beasts-architecture-and-taom-port.md)).

### Where models live + are resolved
- **Campaign models** — `Campaign.Current.Models` (`GameModels : GameModelsManager`, CampaignSystem.cs:43100):
  typed accessors (`Models.PartyWageModel`, `Models.SettlementLoyaltyModel`, …) → `GetModel<T>()`. Used for campaign
  calculations (wages, loyalty, party speed, prosperity, …).
- **Mission models** — `MissionGameModels.Current` (`MissionGameModels : GameModelsManager`, MountAndBlade.cs:75691):
  in-battle calculations (`AgentStatCalculateModel`, `AgentApplyDamageModel`, `BattleBannerBearersModel`, …).
- Both pull from the **`GameStarter`'s `_models`** registered at `OnGameStart`: `gameStarter.AddModel(...)`. So a mod
  registers everything via the starter; the campaign + mission managers resolve from it by type.

### Registration order matters
The vanilla defaults are added when the campaign/mission game type initializes. A mod must `AddModel` its overrides
**after** the defaults so they're later in `_models` and win `GetModel<T>()`. `SubModule.OnGameStart(game,
gameStarter)` runs after the base game registers its models, so TAOM adds its `TaomXxxModel`s there.

## WHY it's shaped this way

Last-wins-by-list-order + subtype matching gives a dead-simple override model: a mod just appends a subclass and it
takes precedence, while the base instance remains for the subclass to `base`-call. No registry mutation, no removal,
no priority numbers. The decorator overload exists for mods that can't subclass (sealed base) or want to wrap an
unknown previous model.

## TAOM relevance + gotchas
- **TAOM overrides 70 models on 2026-11-03** ([gamemodel-registry.md](../gamemodel-registry.md), the parked and abstract ones included): `TaomPartyWageModel`,
  `TaomPartySpeedModel`, `TaomSettlementLoyaltyModel`, `TaomBattleRewardModel`, `TaomAgeModel`, …, each
  `: DefaultXxxModel` (or the `Sandbox*`/`CustomBattle*` concrete for that slot), overriding specific methods + calling `base`. Registered in `Main/SubModule.cs OnGameStart`
  via `campaignStarter.AddModel(new TaomXxxModel(...))`, or declared by a feature module with `GameModelDecl.Of<TSlot, TModel>(...)`, which `FeatureModuleHooks` adds after SubModule's own.
- **One override per model type** — last-wins. If two TAOM models subclass the same `DefaultXxxModel`, only the
  last-added wins. This is why TAOM **consolidates** several concerns into **one** override of a shared slot: e.g.
  `TaomAgentStatCalculateModel` folds the creature mount-locks **and** the career agent-stat passives (plus culture aggression, race abilities and the rest of its registry row) into a single
  `AgentStatCalculateModel` override, because there's only one slot.
- **The available surface:** any `DefaultXxxModel` TAOM hasn't overridden is fair game to override (subclass +
  `AddModel`). Browse the `Default*Model` classes in the decompile (`Campaign/…/GameComponents/`) for unclaimed ones.
- **Always call `base`** for the cases you don't handle (the GameModel Override Pattern returns `?? base.X(...)`),
  or you silently drop vanilla behavior. And **no inline branching in the override** — delegate to a service
  ([gamemodels-services lessons](../../reviews/lessons/gamemodels-services.md), "GameModel override bodies allow no inline decision logic...").
- **Register after defaults** (OnGameStart) — registering too early (before the base model exists) means yours is
  *before* the default in `_models` and the default wins.
- **Non-virtual setters on a model.** `AgentStatCalculateModel.SetAILevelMultiplier(float)` and `ResetAILevelMultiplier()` are not
  virtual; they write a private `_AILevelMultiplier` that the same class reads when it computes the AI level
  (`AgentStatCalculateModel.cs:12, 138-146, 171-172` [TAOM-verified, v1.5.7 cache]). A decorator model (the `AddModel<T>` form above)
  that forwards to `BaseModel` keeps the value its callers set on the wrapper, while `BaseModel` computes from its own field.
  yotthani's SmithingExtended `WearAgentStatModel` reads the private field with a compiled expression and copies it into `BaseModel`
  before each delegation (`HoN/SmithingExtended/Integration/Wear/WearAgentStatModel.cs@c45d8374:14`, tournaments call the setters;
  read from code, not run). TAOM's own models are not hit: `TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel`
  (`CareerSystem/Models/TaomAgentStatCalculateModel.cs:28`) and `TaomCustomBattleAgentStatCalculateModel :
  CustomBattleAgentStatCalculateModel` (`CultureDoctrine/Models/...:30`) inherit and call `base`, and neither vanilla base has a
  `BaseModel` reference. Only another mod that wraps a TAOM model is exposed, and SmithingExtended already copies the field.
- **A decorator's `BaseModel` can be null.** yotthani's TroopStatus falls back to a new `Default` model when `BaseModel` is null
  ("registration ordering, non-generic AddModel overload") and returns a constant when `Campaign.Current` is null, because models can run
  during character creation (`HoN/TroopStatus/TroopStatusPartyMoraleModel.cs@c45d8374:22-37, 87-92`, read from code). TAOM has no site
  for the first half: Grep of `\bBaseModel\b` in `Main/` and `Client/` `*.cs` on 2026-11-13 gave 5 hits, all comments, so no TAOM code
  dereferences it. The `Campaign.Current == null` half was not checked.

## The native boundary
The GameModel system is **managed** — registration, resolution, and the model methods are all C# (the formulas are
managed). Models may read native agent/party state, but the model framework + the override mechanism are pure
managed, which is why it's the clean extension point.

## Evidence (file:line, v1.4.5)
- `TaleWorlds.Core.cs`:14826 (`GameModel`), 14829 (`GameModelsManager`).
- `TaleWorlds.MountAndBlade.cs`:61602 (`GetModel<T>` backward scan = last-wins), 61614 (`AddModel(GameModel)`), 61619 (`AddModel<T>(MBGameModel<T>)` decorator), 75691 (`MissionGameModels`).
- `TaleWorlds.CampaignSystem.cs`:43100 (`GameModels` = `Campaign.Current.Models`), 23206/23218/23223 (campaign-side `GetModel`/`AddModel`).
- TAOM: `Main/SubModule.cs OnGameStart` `AddModel` calls; `.claude/rules/gamemodels.md` + `csharp-patterns.md` (the Override Pattern); `docs/reference/gamemodel-registry.md` (the override table).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
