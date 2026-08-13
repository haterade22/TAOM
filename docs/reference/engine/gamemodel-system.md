# Bannerlord GameModel system — how overrides plug in (Phase 7)

> **One process, traced from the decompile** (v1.4.5): how `GameModel`s are registered + resolved, and the
> override mechanism behind TAOM's ~40 `TaomXxxModel : DefaultXxxModel` overrides (the CLAUDE.md GameModel table —
> the mod's single biggest extension surface). Part of the phased engine study.

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
- **TAOM overrides ~40 models** (CLAUDE.md "GameModel Overrides" table): `TaomPartyWageModel`,
  `TaomPartySpeedModel`, `TaomSettlementLoyaltyModel`, `TaomBattleRewardModel`, `TaomAgeModel`, …, each
  `: DefaultXxxModel`, overriding specific methods + calling `base`. Registered in `Main/SubModule.cs OnGameStart`
  via `campaignStarter.AddModel(new TaomXxxModel(...))`.
- **One override per model type** — last-wins. If two TAOM models subclass the same `DefaultXxxModel`, only the
  last-added wins. This is why TAOM **consolidates** several concerns into **one** override of a shared slot: e.g.
  `TaomAgentStatCalculateModel` folds the elephant mount-lock **and** the career agent-stat passives into a single
  `AgentStatCalculateModel` override (Phase via the elephant work) — there's only one slot.
- **The available surface:** any `DefaultXxxModel` TAOM hasn't overridden is fair game to override (subclass +
  `AddModel`). Browse the `Default*Model` classes in the decompile (`Campaign/…/GameComponents/`) for unclaimed ones.
- **Always call `base`** for the cases you don't handle (the GameModel Override Pattern returns `?? base.X(...)`),
  or you silently drop vanilla behavior. And **no inline branching in the override** — delegate to a service
  (`feedback_gamemodel_inline_logic`).
- **Register after defaults** (OnGameStart) — registering too early (before the base model exists) means yours is
  *before* the default in `_models` and the default wins.

## The native boundary
The GameModel system is **managed** — registration, resolution, and the model methods are all C# (the formulas are
managed). Models may read native agent/party state, but the model framework + the override mechanism are pure
managed, which is why it's the clean extension point.

## Evidence (file:line, v1.4.5)
- `TaleWorlds.Core.cs`:14826 (`GameModel`), 14829 (`GameModelsManager`).
- `TaleWorlds.MountAndBlade.cs`:61602 (`GetModel<T>` backward scan = last-wins), 61614 (`AddModel(GameModel)`), 61619 (`AddModel<T>(MBGameModel<T>)` decorator), 75691 (`MissionGameModels`).
- `TaleWorlds.CampaignSystem.cs`:43100 (`GameModels` = `Campaign.Current.Models`), 23206/23218/23223 (campaign-side `GetModel`/`AddModel`).
- TAOM: `Main/SubModule.cs OnGameStart` `AddModel` calls; `.claude/rules/gamemodels.md` + `csharp-patterns.md` (the Override Pattern); CLAUDE.md GameModel table (~40 overrides).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
