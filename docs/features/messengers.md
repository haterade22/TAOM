# Messengers

## Overview

Lets the player dispatch a paid messenger to any reachable hero — either from the encyclopedia hero page or from an in-person dialog. The messenger travels for N in-game days at a speed scaled to the campaign map; on arrival the player gets a "Speak / Dismiss" inquiry that opens a real conversation mission. Settlement-aware: if the target is in a settlement the conversation enters that settlement's scene and the player is teleported back to the map afterward; otherwise it opens as a field conversation.

Ported from LOTRAOM (Bannerlord 1.2.12) with TAOM conventions (adapter discipline, primitive-dict save, MCM-via-`TaomSettings`, JSON-validated advanced tunables, no `SaveableTypeDefiner`).

## Why This Exists

- **Vanilla behavior:** No fast-travel or async cross-map communication. Talking to any hero requires physically intercepting their party or finding them in a settlement.
- **TAOM requirement:** TAOM disables fast-travel and the LOTR-flavored campaign relies heavily on cross-map diplomacy (alliances, named-companion recruitment, lord conversations). Hunting heroes across the map is friction without payoff.
- **Without this feature:** Players spend long stretches chasing lords for one-line conversations, or simply skip diplomacy and miss content.

## Architecture

### Design Challenge

- The messenger model is "queue → travel → arrive → conversation," but the conversation must reuse the engine's full conversation/encounter/mission stack — there is no headless way to fire dialog. That forces a `IMissionListener` implementation plus careful interleaving of `PlayerEncounter` lifecycle calls.
- A messenger to a hero in a settlement must enter that settlement's scene (otherwise the conversation is locationless) and the player must be returned to the original map position when the mission ends. That requires a one-shot `TickEvent` listener after `OnEndMission` (you cannot tear down a `PlayerEncounter` mid-iteration of mission listeners).
- Save-compat must survive across sessions: a messenger sent on day 100 must continue toward arrival even if the player saves and reloads on day 102. TAOM's convention is primitive-dict save (no `SaveableTypeDefiner`), so the entire pending list is serialised as `Dictionary<heroId, "dispatchDays|posX|posY|arrivedFlag">`.
- Pre-flight validation has 11 rejection paths (dead, prisoner, fugitive, child, in player party, etc.). Putting all of those in the campaign behavior would explode the boundary class. Validation lives in the service via a `HeroSnapshot` POCO that the behavior populates at the boundary.
- Bannerlord 1.3.15 introduced API breaks vs LOTRAOM's 1.2.12: `IMissionListener.OnInitialDeploymentPlanMade(BattleSideEnum, bool)` removed → replaced by `OnDeploymentPlanMade(Team, bool)`; `TextObject.Empty` removed → `TextObject.GetEmpty()`; `MobileParty.Position2D` setter removed → `MobileParty.Position = new CampaignVec2(vec, isOnLand)`; `IMapPoint.Position2D` (Vec2) renamed to `IMapPoint.Position` (CampaignVec2 — needs `.ToVec2()`); `CampaignTime` ctor became internal — use `CampaignTime.Now.ToDays` for elapsed math.

### Solution Approach

Layered TAOM architecture. The behavior is the TaleWorlds-bound boundary; the service is pure logic; the store owns mutable state; the config provider validates JSON tunables; the settings provider wraps MCM.

- **`MessengerCampaignBehavior`** (entry point, ~370 lines) — registers events (`HourlyTickEvent`, `OnSessionLaunchedEvent`), implements `IMissionListener`, registers the dialog tree, orchestrates encounter routing (settlement vs field), and serialises state via `SyncData`. Touches sealed types directly because that's what the boundary is for.
- **`MessengerService`** (Reuse.Singleton) — pure logic: `CanSendMessenger(HeroSnapshot, playerGold) → MessengerValidationResult`, `RollAccident()`, `AdvancePosition(current, target, speed) → PositionUpdate`, `CalculateMessengerSpeed(mapDiagonal, travelDays)`. Tested with 26 unit tests against NSubstitute mocks.
- **`MessengerStateStore`** — owns the in-memory `Dictionary<heroId, PendingMessenger>`, exposes CRUD + `Serialize`/`Deserialize` for the behavior's `SyncData`.
- **`MessengerConfigProvider`** — validates `messenger_config.json` per the "Config Providers MUST Validate" rule (range-check + revert + warn).
- **`MessengerSettingsProvider`** — wraps `TaomSettings.Instance.*` so the service can be mocked in tests.
- **`MessengerEncyclopediaPrefabExtension` + `MessengerEncyclopediaMixin`** — UIExtenderEx integration. The prefab extension appends a `<ListPanel>` containing a "Send Messenger" `<ButtonWidget>` after the `RichTextWidget[@Text='@InformationText']` in `EncyclopediaHeroPage`. The mixin exposes `IsMessengerAvailable`, `SendMessengerCost`, `SendMessengerHint`, `SendMessengerActionName` as data sources, and `ExecuteSendMessenger` as the click command.

### Component Diagram

```
TaomSettings.cs (MCM)            messenger_config.json (advanced)
        |                                  |
        v                                  v
MessengerSettingsProvider     MessengerConfigProvider
        |                                  |
        |     +----------------------------+
        v     v
+-> MessengerService <-> MessengerStateStore <- IMessengerRandomSource
|       (pure logic)         (CRUD, save)         (MBRandom wrapper)
|         |  ^
|         |  |
|         v  |
+ MessengerCampaignBehavior  ----> Mission, PlayerEncounter,
  (registers events,                LocationComplex, Hero, MobileParty,
   IMissionListener,                ConversationCharacterData
   dialog tree, SyncData)
        |
        v
  Dialog tree (hero_main_options -> ... -> close_window)

MessengerEncyclopediaPrefabExtension  --[XPath append]-->  EncyclopediaHeroPage
MessengerEncyclopediaMixin            --[ViewModelMixin("RefreshValues")]-->  EncyclopediaHeroPageVM
        |
        +-> calls IMessengerService for validation
        +-> calls Campaign.Current.GetCampaignBehavior<MessengerCampaignBehavior>().SendMessenger(hero) on click
```

## Configuration

### MCM (player-facing) — [TaomSettings.cs](../../Main/Features/TaomSettings.cs)

Group: **Messengers**

| Setting | Type | Range | Default | Effect |
|---------|------|-------|---------|--------|
| `EnableMessengers` | bool | — | true | Master toggle. When off, the behavior is not registered and the encyclopedia button hides itself. **Requires restart to take effect** (Singleton lifetime). |
| `MessengerGoldCost` | int | 10–500 | 50 | Denar cost per dispatch. |
| `MessengerTravelDays` | int | 1–10 | 3 | In-game days a messenger spends in transit. Speed scales to map size: `mapDiagonal / (24 * travelDays) * speedMultiplier`. |
| `MessengerAccidents` | bool | — | true | Master toggle for the random ambush roll. |

### JSON config — [messenger_config.json](../../Main/_Module/ModuleData/messengers/messenger_config.json)

Advanced tunables that are not exposed in MCM.

```json
{
  "accidentChancePerHour": 0.002,
  "travelSpeedMultiplier": 1.0
}
```

| Field | Range | Default | Effect |
|-------|-------|---------|--------|
| `accidentChancePerHour` | [0.0, 1.0] | 0.002 | Per-hour ambush probability (gated by MCM `MessengerAccidents`). 0.002 = ~0.2% per hour, ~5% chance over a 24-hour leg. |
| `travelSpeedMultiplier` | [0.1, 10.0] | 1.0 | Multiplies the per-hour travel speed. Use to retune travel without changing in-game `TravelDays`. |

Out-of-range values revert to defaults with a warning. **JSON config requires a Bannerlord application restart to reload** — `MessengerConfigProvider` is `Reuse.Singleton` and caches the config at first access for the entire process lifetime.

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/Messengers/IMessengerService.cs](../../Main/Features/Messengers/IMessengerService.cs) | Service contract (validation, accident roll, position math, speed calc) |
| [Main/Features/Messengers/MessengerService.cs](../../Main/Features/Messengers/MessengerService.cs) | Pure-logic implementation |
| [Main/Features/Messengers/IMessengerStateStore.cs](../../Main/Features/Messengers/IMessengerStateStore.cs) | Pending-messenger CRUD + serialise/deserialise |
| [Main/Features/Messengers/MessengerStateStore.cs](../../Main/Features/Messengers/MessengerStateStore.cs) | In-memory dict with primitive-string roundtrip |
| [Main/Features/Messengers/IMessengerConfigProvider.cs](../../Main/Features/Messengers/IMessengerConfigProvider.cs) | JSON config contract |
| [Main/Features/Messengers/MessengerConfigProvider.cs](../../Main/Features/Messengers/MessengerConfigProvider.cs) | Validating loader (range-check, revert, warn) |
| [Main/Features/Messengers/MessengerConfig.cs](../../Main/Features/Messengers/MessengerConfig.cs) | Config POCO + defaults |
| [Main/Features/Messengers/IMessengerSettingsProvider.cs](../../Main/Features/Messengers/IMessengerSettingsProvider.cs) | MCM-wrapper contract |
| [Main/Features/Messengers/MessengerSettingsProvider.cs](../../Main/Features/Messengers/MessengerSettingsProvider.cs) | Reads `TaomSettings.Instance.*` |
| [Main/Features/Messengers/IMessengerRandomSource.cs](../../Main/Features/Messengers/IMessengerRandomSource.cs) | Random-float contract (for accident roll testability) |
| [Main/Features/Messengers/MessengerRandomSource.cs](../../Main/Features/Messengers/MessengerRandomSource.cs) | `MBRandom.RandomFloat` wrapper |
| [Main/Features/Messengers/Domain/HeroSnapshot.cs](../../Main/Features/Messengers/Domain/HeroSnapshot.cs) | Boundary POCO (10 hero properties) for validation |
| [Main/Features/Messengers/Domain/MessengerValidationResult.cs](../../Main/Features/Messengers/Domain/MessengerValidationResult.cs) | 11-value rejection enum |
| [Main/Features/Messengers/Domain/PendingMessenger.cs](../../Main/Features/Messengers/Domain/PendingMessenger.cs) | Saveable POCO + `Serialize`/`TryDeserialize` |
| [Main/Features/Messengers/Domain/PositionUpdate.cs](../../Main/Features/Messengers/Domain/PositionUpdate.cs) | Service return struct (new position + arrived flag) |
| [Main/Features/Messengers/MessengerCampaignBehavior.cs](../../Main/Features/Messengers/MessengerCampaignBehavior.cs) | Boundary: dialog tree, IMissionListener, encounter routing, `SyncData` |
| [Main/Features/Messengers/MessengerIoC.cs](../../Main/Features/Messengers/MessengerIoC.cs) | DryIoc registrations |
| [Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs](../../Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs) | UIExtenderEx prefab patch (button injection) |
| [Main/Features/Messengers/UI/MessengerEncyclopediaMixin.cs](../../Main/Features/Messengers/UI/MessengerEncyclopediaMixin.cs) | UIExtenderEx VM mixin (data sources + click command) |
| [Main/_Module/ModuleData/messengers/messenger_config.json](../../Main/_Module/ModuleData/messengers/messenger_config.json) | Advanced tuning |
| [Main/_Module/ModuleData/taom_messenger_strings.xml](../../Main/_Module/ModuleData/taom_messenger_strings.xml) | EN base — 29 keys |
| `Main/_Module/ModuleData/Languages/<LANG>/std_taom_messenger_strings_*.xml` | 12 language variants (BR/CNs/CNt/DE/FR/IT/JP/KO/PL/RU/SP/TR) — English placeholder text where untranslated |

## Dependencies

- `IMessengerSettingsProvider` — wraps `TaomSettings.Instance` for the 4 MCM toggles
- `IMessengerConfigProvider` — wraps `messenger_config.json`
- `IMessengerStateStore` — owns the pending-messenger collection
- `IMessengerRandomSource` — wraps `MBRandom.RandomFloat`
- `IModLogger` (Core) — used by `MessengerConfigProvider` and `MessengerStateStore` for warnings
- `IPathService` (Core) — used by `MessengerConfigProvider` to locate `messenger_config.json`
- TaleWorlds (boundary only): `Hero`, `MobileParty`, `Settlement`, `PartyBase`, `PlayerEncounter`, `LocationComplex`, `Mission`, `IMissionListener`, `ConversationCharacterData`, `CampaignTime`, `CampaignVec2`, `Vec2`, `EnterSettlementAction`, `GiveGoldAction`, `CampaignEvents`, `CampaignEventDispatcher`, `MBTextManager`, `InformationManager`

## Tests

55 unit tests across 3 files:

- [TAOM.Tests/Features/Messengers/MessengerServiceTests.cs](../../TAOM.Tests/Features/Messengers/MessengerServiceTests.cs) — 26 tests covering all 11 `MessengerValidationResult` rejection paths, accident roll boundaries (p=0/1/disabled), position math edge cases (distance < / = / > speed; invalid target), speed calculation (zero/negative days clamping, multiplier).
- [TAOM.Tests/Features/Messengers/MessengerStateStoreTests.cs](../../TAOM.Tests/Features/Messengers/MessengerStateStoreTests.cs) — 15 tests covering Add/Remove/Get/Contains/Clear, duplicate-id replacement, null/empty input, save roundtrip preserves all fields, malformed-on-load drops + warns.
- [TAOM.Tests/Features/Messengers/MessengerConfigProviderTests.cs](../../TAOM.Tests/Features/Messengers/MessengerConfigProviderTests.cs) — 14 tests covering valid JSON, missing file, malformed JSON, every validation rule (negative chance, >1.0 chance, zero/negative speed mult, absurd speed mult), boundary values, partial JSON merge, caching, info-vs-warning logging.

The boundary classes (`MessengerCampaignBehavior`, UI mixin/prefab, `MessengerSettingsProvider`, `MessengerRandomSource`, `MessengerIoC`) follow TAOM's "boundaries are not unit-tested" convention and are validated through manual game-testing (see GitHub issue #109 manual test plan).

## How to add a new validation rejection reason

1. Add the new value to `MessengerValidationResult` (e.g., `HeroRetired`).
2. Add a corresponding skip-guard in `MessengerService.CanSendMessenger` returning the new value.
3. Add a `case` to `MessengerCampaignBehavior.BuildValidationReason` that returns the player-facing `TextObject` with `{HERO_NAME}` substitution if needed.
4. Add the localization key (`taom_messenger_<reason>`) to all 13 string files (1 EN base + 12 language variants).
5. Add a unit test in `MessengerServiceTests` named `CanSendMessenger_<NewState>_Returns<NewEnum>`.
6. Optionally extend `HeroSnapshot` if the new check needs a property not already exposed.

## How to add a new advanced tuning knob to messenger_config.json

1. Add the property to `MessengerConfig.cs` with its default value.
2. Add a validation block to `MessengerConfigProvider.Validate` (range-check, sign-check, revert-with-warning per the rule).
3. Consume the new property in `MessengerService` (or wherever the consumer lives).
4. Add a unit test in `MessengerConfigProviderTests` named `GetConfig_<InvalidVariant>_RevertsToDefaultAndWarns` for each validation rule, plus a boundary test.
5. Document the new field + range in the "JSON config" table above.

## Performance

- `OnHourlyTick` uses a reusable `_toRemoveScratch` field (cleared per tick) instead of allocating `new List<string>()` each campaign hour.
- `IMessengerStateStore.GetAll()` returns the live `Dictionary<,>.ValueCollection` (zero allocation).
- `MessengerEncyclopediaMixin` caches the four state-independent `HintViewModel` instances at construction (`_emptyHint`, `_hintTargetUnavailable`, `_hintMessengersDisabled`, `_hintSystemUnavailable`); the rejection-reason hint is keyed by `MessengerValidationResult` and only re-allocated when the rejection class transitions.
- `MessengerConfigProvider` and `MessengerSettingsProvider` are `Reuse.Singleton`; config is loaded once via `Lazy<>` at first access.

## Changelog

- 2026-05-13 — Wired Messengers into IoC + campaign starter (fixed encyclopedia hero-click NRE, #121) and added a wiring regression test (#191); fixed UI mixin notifications firing on self (#166) and per-campaign/arrival state-reset gaps (#123).
- 2026-05-06 — Ported the LOTRAOM messenger system to TAOM (1.3.15): paid messenger dispatch from the encyclopedia/dialog, travel + arrival conversation routing, primitive-dict save, MCM + JSON tunables, 12-language localization (#109).

## GitHub Issue

- **Issue:** [#109 — feat(messengers): port LOTRAOM messenger system to TAOM (1.3.15)](https://github.com/haterade22/TAOM/issues/109)
- **Status:** Open (closes after Codex pass + game-test sign-off)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
