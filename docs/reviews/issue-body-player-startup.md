## Motivation

LOTRAOM 1.2.12 ships a `StartingEquipmentGold` feature that grants the player **starting funds** at character creation finalize and **persists the youth option's equipment roster** onto the player hero. TAOM (1.3.15) had only half of this:

- NPC lord startup gold + influence — `StartupResources` feature, XML-driven, working
- Per-youth equipment **preview** — `NarrativeMenuBuilder.UpdateYouthEquipment` mutates the CC preview character
- **Player starting funds** — MISSING. `StartupGoldService.cs:40` explicitly skips player clan: `if (hero.IsPlayerClan) continue;`
- **Equipment persistence** — MISSING. Preview-only; nothing copies the roster onto `Hero.MainHero.BattleEquipment` / `CivilianEquipment` at finalize

Result: every TAOM campaign started the player with vanilla default 1000 denars and vanilla default starting equipment regardless of culture or youth option. This issue ports the LOTRAOM feature to TAOM 1.3.15.

## Design

- **Configurable** per-culture player gold via existing `startup_resources_config.xml` (new `playerGold` attribute), validated `[0, 10_000_000]` per the "Config Providers MUST Validate" rule.
- **Adapter pattern (ADR-007)** — new `IPlayerEquipmentAdapter` returning a `PlayerEquipmentApplyResult` enum so the service surface stays free of sealed `Hero` / `Equipment` / `MBEquipmentRoster` types.
- **Reuse** — existing `IGoldGiftAdapter` already wraps `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, true)` exactly the way LOTRAOM calls it. No new gold adapter needed.
- **Roster ID convention reuse** — TAOM already had the roster ID format `player_char_creation_{culture}_{titleType}_{m|f}` inlined in `NarrativeMenuBuilder.BuildEquipmentRosterId`. Promoted to a shared helper `PlayerEquipmentRosterIds.Build` so the visual preview, the persistence at finalize, and the career-screen preview all derive the same ID.
- **Wiring** — both new services injected into `CharacterCreationContentService` and called from `OnCharacterCreationFinalize` after `AssignCareer`. Each call exception-isolated so a failure in one does not block the other.
- **XML/JSON-driven** (not LOTRAOM hardcoded C# dictionary) so values are tunable without recompile.

**Alternatives considered + rejected:**

- *Per-youth-option gold field in youth_menu.json* — rejected this session (user picked per-culture only; simpler config, fewer tuning surfaces).
- *Adding a second gold-giving adapter* — rejected; existing `IGoldGiftAdapter.GiveGoldToHero` already passes `null` from-hero exactly like LOTRAOM.
- *Mutating equipment via reflection on `Hero._battleEquipment`* — rejected; `Hero.BattleEquipment.FillFrom(roster)` is the public v1.3 surface.

## Implementation

### New files

| Path | Purpose |
|------|---------|
| `Main/Adapters/IPlayerEquipmentAdapter.cs` | Returns `PlayerEquipmentApplyResult` enum (Success / RosterNotFound / NoSuitableEquipment / HeroNotFound) |
| `Main/Adapters/PlayerEquipmentAdapter.cs` | Wraps `MBObjectManager.GetObject<MBEquipmentRoster>`, filters by `IsBattle`/`IsCivilian`, applies via `Hero.BattleEquipment.FillFrom` / `Hero.CivilianEquipment.FillFrom`. Guards each slot against its dedicated dead-equipment singleton. |
| `Main/Features/StartupResources/IPlayerStartupGoldService.cs` + impl | Looks up `PlayerGold` from config, calls `IGoldGiftAdapter.GiveGoldToHero` |
| `Main/Features/CharacterCreation/IPlayerEquipmentService.cs` + impl | Builds roster ID via shared helper, dispatches to adapter, switches over the 4 result enum values |
| `Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs` | Shared helper `Build(cultureId, titleType, isFemale)` |

### Modified files

| Path | Change |
|------|--------|
| `Main/Features/StartupResources/Config/StartupResourcesConfig.cs` | Added `int PlayerGold` to `CultureResourceEntry` |
| `Main/Features/StartupResources/StartupResourcesConfigProvider.cs` | `ParsePlayerGold` private method, range `[0, 10_000_000]`, rejects negative/over-cap/non-numeric, defaults to 0 silently when missing |
| `Main/Features/StartupResources/StartupResourcesIoC.cs` + `Main/Features/CharacterCreation/CharacterCreationIoC.cs` | New singleton registrations |
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | Two new constructor params; new `GrantPlayerStartupResources` private method invoked from `OnCharacterCreationFinalize` after `AssignCareer`; each service call wrapped in try/catch |
| `Main/Features/CharacterCreation/NarrativeMenuBuilder.cs` | `BuildEquipmentRosterId` delegates to the shared `PlayerEquipmentRosterIds.Build` |
| `Main/Features/CharacterCreation/CareerMenuService.cs:227` | Same delegation (deep-review fix; was inlining the format string) |
| `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml` | `playerGold` attribute on 18 cultures (Elven 8000–10000, Dwarf 7500, Dark 6000, Human Good 5000, Tribal/Eastern incl. shaghana/abanissa 4000) |
| `Main/_Module/ModuleData/charactercreation/youth_menu.json` | `taom_youth_sturgia_1` `title_type` retainer→guard (no vanilla `sturgia_retainer` roster) |
| `CHANGELOG.md`, `docs/features/startup-resources.md`, `docs/features/character-creation.md`, `docs/features/kingdom-creation.md` | Documentation updates |

## Testing

**85/85** session-targeted tests pass. **1340/1340** total project tests pass. Build clean.

- **5 new** `StartupResourcesConfigProviderTests` cases — `playerGold` happy-path parse, negative rejected, over-cap rejected, non-numeric rejected, missing-attribute silent default
- **8 new** `PlayerStartupGoldServiceTests` — culture match (case-insensitive), unknown culture warns, zero-gold skip, null/empty culture/hero no-ops, info-log content
- **9 new** `PlayerEquipmentServiceTests` — male/female roster suffix, null/empty input no-ops, all 4 `PlayerEquipmentApplyResult` branches mapped to correct log levels
- **6 existing** `CharacterCreationContentServiceTests` — updated for new constructor signature

**v1.3.15 API verification** done via `ilspycmd` against installed DLLs at `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\` before any code:
- `GiveGoldAction.ApplyBetweenCharacters(Hero, Hero, int, bool disableNotification = false)` ✓
- `MBEquipmentRoster.AllEquipments` returning `MBReadOnlyList<Equipment>` ✓
- `Equipment.IsBattle` / `Equipment.IsCivilian` / `Equipment.FillFrom(Equipment, bool useSourceEquipmentType = true)` ✓
- `Hero.BattleEquipment` (→ `Campaign.Current.DeadBattleEquipment` fallback) ✓
- `Hero.CivilianEquipment` (→ `Campaign.Current.**DeadCivilianEquipment**` fallback — separate singleton, NOT `DeadBattleEquipment`) ✓
- `Hero.FindFirst(Func<Hero, bool>)`, `MBObjectManager.GetObject<T>(string)`, `CharacterCreationContent.SelectedTitleType { get; set; }` ✓

## Review process

This work passed the full TAOM completion workflow Phases 1–3:

- **Phase 1** — `/verify` build + tests green
- **Phase 1** — `/deep-review` (5 parallel Claude agents). Surfaced 4 fixes, all applied this session: `empire`/Dunland missing from XML; `taom_youth_sturgia_1` title_type retainer→guard; `CareerMenuService:227` route through helper; defensive `DeadBattleEquipment` guard
- **Phase 2** — `/codex-verify` (independent Codex review with `xhigh` reasoning, ran via `codex review` CLI). Caught **1 P1** + **1 P2** that Claude's deep-review missed:
  - **P1** Civilian-equipment guard targeted `DeadBattleEquipment` instead of `DeadCivilianEquipment` (Claude API agent reported the wrong fallback target)
  - **P2** `shaghana` and `abanissa` kingdoms missing from XML (Claude data-flow agent flagged but dismissed as "may be intentional")
  - Both fixed this session.
- **Phase 3 (user-driven)** — User pointed out that `shaghana` and `abanissa` are full **independent kingdoms** in the Harad region, not "Aserai-region cultures with no NPC clans" as initially misclassified. Fixed: 17 NPC lords across both kingdoms now receive proper startup gold/influence (`gold="50000" influence="100"`).

## Risks / known limitations

- **Tuning ranges are seeds, not balanced values.** Numbers like Mordor=6000, Rivendell=10000 reflect a tier rationale (dark factions arm recruits well, Elven wealth) but haven't been play-tested for snowball risk against per-culture feats and economy modifiers.
- **No MCM live retuning.** Singleton config cache means edits require Bannerlord process restart, not save-load. Documented in feature doc.
- **Pre-existing tech debt** flagged by deep-review: `CharacterCreationContentService.AssignCareer` resolves `ICareerCreationHandler` and `ICareerRegistry` via `IoC.Resolve<>` (lines ~218, 235) — service-locator anti-pattern. Pre-dates this session. Should be lifted to constructor injection in a follow-up issue.

## Plan

`C:\Users\mikew\.claude\plans\please-investigate-this-that-lovely-pine.md` (local file)
