# Wanderer Allegiance

## Overview

A wanderer will not take service across the Free/Evil line. When the player asks a tavern wanderer
"I can use someone like you in my company", a Free-culture wanderer (Gondor, Rohan, the Elves,
Erebor, Dale, Lindon) refuses a player who serves Sauron, and an Evil-culture wanderer (Mordor,
Isengard, the orc kingdoms, Harad, Rhûn) refuses a player on the Free side. Neutral cultures
(Umbar, Dunland, Shaghana, Abanissa) serve anyone, and a Neutral player is refused by nobody. The
refusal is spoken in the dialogue and the conversation returns to the wanderer's menu; no gold moves.

Two dialogue lines and a pure rule. No Harmony patch, no GameModel. Issue #575, reported as
"wanderers like Aragorn end up in evil clans".

## Why This Exists

- **Vanilla behavior:** the hire is one player line into the `companion_hire` token
  (`LordConversationsCampaignBehavior.cs:797`, installed v1.4.8), one NPC reply at the default
  priority 100 (`:820`, whose condition `conversation_companion_hire_gold_on_condition` only sets
  text variables and always returns true), and a consequence (`:2028`) that pays the wanderer and
  calls `AddCompanionAction.Apply(Clan.PlayerClan, hero)`. Nothing asks which side either party is
  on. A Mordor character can hire Aragorn in Bree.
- **TAOM requirement:** the named companions carry their lore cultures (`named_companion_aragorn`
  is `Culture.gondor`, Legolas `mirkwood`, Gimli `erebor`, Maztog and BlackRose `isengard`), and
  every generic tavern wanderer in `taom_wanderers.xml` carries a culture too. The Free/Evil table
  those cultures are classified in (`execution/alignment.json`) already gates recruitment, marriage,
  desertion and prisoner morale. Hiring was the one companion path it did not reach.
- **Without this feature:** Aragorn, Legolas and Gimli serve Sauron's vassals, and an Uruk serves
  Gondor, whenever a player asks.

### What was ruled out first

The report says "evil clans", so the AI paths were traced before touching the dialogue. In v1.4.8
there is no engine path that moves a wanderer into a non-player clan:

| Path | Why a wanderer cannot take it |
|---|---|
| `AddCompanionAction.Apply` | Three call sites in the whole dump, all `Clan.PlayerClan` (`LordConversationsCampaignBehavior:2030`, `LordsNeedsTutorIssueBehavior:437`, `FamilyFeudIssueBehavior:1724`) |
| AI party leader (`HeroSpawnCampaignBehavior.GetBestAvailableCommander`) | Both loops require `CharacterObject.Occupation == Occupation.Lord` |
| Clan heir (`Clan.GetHeirApparents`) | Excludes `IsWanderer` |
| AI governor (`ClanVariablesCampaignBehavior.UpdateGovernorsOfClan`) | Iterates `AliveLords`; a `CompanionOf` hero lands in the companions cache, never there |
| AI marriage (`MarriageAction`) | `DefaultMarriageModel.IsSuitableForMarriage` requires `IsLord` |
| Rebel and minor-faction heroes | Built from `rebellious_hero_templates` / `minor_faction_character_templates`; no TAOM XML lists a wanderer under either (wanderers appear only under `notable_templates`, as in vanilla) |
| TAOM's own minting (`HeroCommissionAdapter.cs:102`, `WardenService.cs:232`) | Both `AddCompanionAction.Apply(Clan.PlayerClan, ...)` |

So the evil clan in the report is the player's own clan, and the entry point is the vanilla hire
dialogue.

## Architecture

### Design Challenge

The hire is registered by vanilla with delegates on private methods, so the obvious fix is a
Harmony postfix on `conversation_hero_hire_on_condition` plus a same-text TAOM player line leading
to a refusal. That is a patch category, a registry entry, three binding pins and a wiring test, for
a result the dialogue system can give one hop later for free.

### Solution Approach

`ConversationManager.GetSentenceOptions(onlyPlayer: false, processAfterOneOption: false)`
(installed v1.4.8, `ConversationManager.cs:515-575`) walks `_sentences`, which `SortSentences`
(`:482-485`) and `SortLastSentence` (`:487-500`) keep sorted priority-descending, and returns on the
FIRST NPC sentence on the active token whose condition is true (`:536`, `:567`). The `companion_hire`
token is produced only at `:797` and consumed only at `:820` in the whole dump. So two TAOM
`AddDialogLine` entries on `companion_hire` at priority 110, each gated on the verdict, are selected
exactly when a refusal applies and are invisible otherwise:

```
hero_main_options
  -> [vanilla :797, untouched] "I can use someone like you in my company." -> companion_hire
       +-- taom_wa_refuse_free (110)  cond: RefusedByFreeWanderer  -> lord_pretalk
       +-- taom_wa_refuse_evil (110)  cond: RefusedByEvilWanderer  -> lord_pretalk
       +-- vanilla companion_hire (100)  otherwise                 -> player_companion_hire_response
lord_pretalk -> hero_pretalk_2 (:767, no condition) "Is there anything else?" -> hero_main_options
```

Vanilla's player line is untouched, so vanilla's own gates still run: the partner is a wanderer, not
already the player's companion, has no party, and is not a prisoner
(`conversation_hero_hire_on_condition :1976`, `conversation_wanderer_on_condition :1274`). The
service therefore takes no "is this a wanderer" inputs. `lord_pretalk` is the exit vanilla itself
uses for the two "no deal" rows of this sub-dialogue (`:821` capacity full, `:823` cannot afford), so
the refusal lands on a return path vanilla already supports in the tavern scene and from the
settlement menu's "Talk", which share the token graph.

The priority is explicit rather than relying on registration order: campaign-event listeners
dispatch LIFO, so whose `OnSessionLaunched` registers first is not something to build on.

### The rule

`WandererAllegianceService.Evaluate(wandererHeroId, wandererCultureId, playerKingdomId, playerCultureId)`:

| Step | Result |
|---|---|
| Feature disabled | `Allowed` |
| Scope is "named companions only" and the hero id is not in `named_companion_config.json` | `Allowed` |
| Wanderer culture null or empty | `Allowed` (nobody's enemy; fail open to vanilla) |
| `wandererSide = GetCultureSide(wandererCulture)`, `playerSide = ResolveSide(playerKingdom, playerCulture)` | |
| Either side Neutral, or both equal | `Allowed` |
| Wanderer Free, player Evil | `RefusedByFreeWanderer` |
| Wanderer Evil, player Free | `RefusedByEvilWanderer` |

**Why culture for the wanderer and kingdom-first for the player.** A tavern wanderer has no clan
and no kingdom; culture is the only side they carry, and it is what the lore rule is about. The
player's side is the kingdom they serve: `IAlignmentService.ResolveSide` reads the kingdom first
and falls back to the culture only when the kingdom is missing or Neutral, so a Gondor-born vassal
or mercenary of Mordor is refused by a Gondor wanderer (`Clan.Kingdom` is set for mercenaries too,
`ChangeKingdomAction.cs:65-68`). Kingdomless, the player clan's culture decides; the main hero's
culture is the fallback for a clan with none.

**Neutral semantics.** The service never calls `IAlignmentService.AreEnemyAlignments`: that
predicate treats Neutral as everyone's enemy, which would bar every Umbar, Dunland, Shaghana and
Abanissa wanderer from serving anybody. Here Neutral serves anyone, the same reading
`MarriageAlignmentService` and `RecruitmentAlignmentService` give the table.

**Where the ids come from.** The dialog behavior converts four engine values at the boundary and
nothing deeper sees an engine type: `Hero.OneToOneConversationHero.StringId` and `.Culture.StringId`,
`Clan.PlayerClan.Kingdom?.StringId`, `Clan.PlayerClan.Culture?.StringId ?? Hero.MainHero.Culture?.StringId`.
`Hero.Culture` is a plain field and `Clan.Culture` an auto-property (neither can throw under `?.`; `Hero.MainHero` and `Clan.PlayerClan` are computed statics, which the try/catch covers), and `Hero.Culture` is set for XML heroes by
`SetInitialValuesFromCharacter` (`Hero.cs:2244`, reached from `Deserialize` via `SetCharacterObject`),
so the named companions carry their XML cultures with no `CharacterObject` fallback needed. The
values are read live on every render of the line, never cached, because Player Switcher changes
`MainHero` and `PlayerClan` mid-session. A throw defers to vanilla hiring and logs an error; that is
safe here because nothing has been mutated.

### Component Diagram

```
wanderer_allegiance_config.json        named_companion_config.json
        |                                        |
WandererAllegianceConfigProvider        INamedCompanionConfigProvider
  (validates scope)                       (the lore character ids)
        |                                        |
WandererAllegianceSettingsProvider ----> WandererAllegianceService <---- IAlignmentService
  (MCM over JSON)                          (pure verdict)                (execution/alignment.json)
                                                 ^
                                                 | four string ids
                                  WandererAllegianceDialogBehavior
                                  (two AddDialogLine on companion_hire, priority 110)
```

## Configuration

### Config File: `Main/_Module/ModuleData/wanderer_allegiance/wanderer_allegiance_config.json`

| Field | Type | Description |
|-------|------|-------------|
| `enabled` | bool | Master toggle. Off = vanilla hiring. |
| `scope` | string | `AllWanderers` (every wanderer, by culture) or `NamedCompanionsOnly` (only the ids in `named_companion_config.json`, disabled entries included). Case-insensitive; anything else reverts to `AllWanderers` with a warning, because the consumer branches on this string and a typo must not silently pick a mode. |

Shipped: `{ "enabled": true, "scope": "AllWanderers" }`. The provider is `Reuse.Singleton`, so a
JSON edit needs a full Bannerlord restart.

### MCM: World / Wanderer Allegiance

| Setting | Default | Notes |
|---|---|---|
| Enable Wanderer Allegiance | on | Overrides `enabled`. `RequireRestart = false`, so flipping it applies to the next conversation. |
| Who Refuses | All wanderers (by culture) | Overrides `scope`. Index 0 = all, 1 = named companions only; an unknown index falls back to the JSON value. |

MCM values win when MCM is loaded; the JSON values are the fallback when `TaomSettings.Instance` is
null (early startup, or MCM failed to load).

### Alignment coverage

An unclassified culture resolves to Neutral, which is a silent permit. All 20 cultures on the 210
wanderer templates and all 7 on the 17 named companions are classified in `alignment.json`;
`WandererCultureAlignmentCoverageTests` fails the build if a wanderer culture ever is not.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/WandererAllegiance/WandererAllegianceService.cs` | The pure verdict |
| `Main/Features/WandererAllegiance/IWandererAllegianceService.cs` | Its interface (four string ids in, a verdict out) |
| `Main/Features/WandererAllegiance/WandererHireVerdict.cs`, `WandererAllegianceScope.cs` | The enums |
| `Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs` | The two dialogue lines and the boundary conversion |
| `Main/Features/WandererAllegiance/WandererAllegianceConfig.cs`, `WandererAllegianceConfigProvider.cs` | JSON DTO and validating loader |
| `Main/Features/WandererAllegiance/WandererAllegianceSettingsProvider.cs` | MCM over JSON |
| `Main/Features/WandererAllegiance/WandererAllegianceIoC.cs` | DryIoc registration, called by `WandererAllegianceModule.RegisterServices` |
| `Main/Features/WandererAllegiance/WandererAllegianceModule.cs` | The feature module, listed in `Main/Composition/FeatureModules.cs`: registers the services and declares the dialog behavior, which the module runner adds at campaign start after every hand-wired behavior |
| `Main/Features/TaomSettings.cs` | The `World/Wanderer Allegiance` group (GroupOrder 51) |
| `Main/_Module/ModuleData/wanderer_allegiance/wanderer_allegiance_config.json` | Shipped config |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_wa_refuse_free`, `taom_wa_refuse_evil` |

## Dependencies

- `IAlignmentService` (Features/Execution): the id-to-side table, `GetCultureSide` and `ResolveSide`.
- `INamedCompanionConfigProvider` (Features/NamedCompanions): the lore character ids for the narrow scope.
- `IPathService`, `IModLogger` (Core).

## Tests

`TAOM.Tests/Features/WandererAllegiance/`:

- `WandererAllegianceServiceTests`: the nine side combinations (exactly two refuse, each with the right verdict), disabled, kingdom-first resolution (`ResolveSide` received, `GetCultureSide` not called for the player), `AreEnemyAlignments` never called, null wanderer culture, the named-companions-only scope (listed, unlisted, disabled entry, case, null id, list read once).
- `WandererAllegianceConfigProviderTests`: missing file, malformed JSON, empty object, JSON `null`, unknown and empty and null `scope`, case normalisation, caching, the shipped file.
- `WandererAllegianceSettingsProviderTests`: MCM absent falls back to JSON for both fields, dropdown index mapping, the compiled dropdown default matches the shipped JSON.
- `WandererCultureAlignmentCoverageTests`: every wanderer and named-companion culture is classified; gondor, mirkwood, erebor stay Free and isengard stays Evil.
- `WandererAllegianceWiringTests`: the module is listed once in `FeatureModules.All`, `IoC.cs` no longer registers the feature by hand, the module's service graph resolves its behavior (still a container singleton), both lines sit on `companion_hire`, return to `lord_pretalk`, and pass a priority above 100, and both string ids are registered for translation. `FeatureModulesTests` checks that no module-declared type is also wired by hand in `SubModule.cs`.
- `WandererAllegianceBindingTests` (`BindingVerification`): `AddHeroGeneralConversations` still emits `companion_hire` three times plus `lord_pretalk` and `main_option_faction_hire`; the three vanilla hire conditions still resolve as parameterless bool methods; `CampaignGameStarter.AddDialogLine` still takes an int `priority` defaulting to 100.

The behavior itself needs a live `CampaignGameStarter` and a conversation, so it is verified in game
(the `FieldCommissionDismissDialogBehavior` posture). `SettingRequireRestartPostureTests` covers the
two MCM properties.

## How to verify in game

New campaigns, each a couple of minutes:

1. Mordor character, walk into Bree (`town_EN2`), talk to Aragorn in the tavern, pick "I can use someone like you in my company." Expected: the Free refusal line, then "Is there anything else?", the option list again, gold unchanged. Companion list unchanged.
2. Gondor character, Isengard (`town_isengard`), Maztog: the Evil refusal line.
3. Umbar (Neutral) character, kingdomless: hires a Gondor wanderer and a Mordor wanderer normally.
4. Mordor character and a generic `spc_wanderer_mordor_*` in any tavern: vanilla hire, price and all.
5. Mod Options, TAOM, World / Wanderer Allegiance, master toggle off, back to the tavern: vanilla hire returns without a restart.
6. Who Refuses = Named companions only: a Gondor character hires a generic Mordor wanderer, Maztog still refuses.
7. Gondor character takes a mercenary contract with Mordor, then talks to a Gondor wanderer: refused (kingdom first).

## Non-goals

- Companions handed over by vanilla quests (`LordsNeedsTutorIssueBehavior`, `FamilyFeudIssueBehavior`) bypass the hire dialogue and are not gated.
- TAOM's own companion minting ([field-commission.md](field-commission.md) promotions, [Refuge](field-camp.md) wardens) turns the player's own troops into companions and is not gated.
- Companions already in the clan when the player's allegiance changes stay (decided 2026-09-12; a departure sweep was declined).
- An enlisted player's side is their own clan's, not their commander's kingdom: [enlistment](enlistment.md) never sets `Clan.PlayerClan.Kingdom`.
- Player Switcher adoption of a wanderer is a takeover, not a hire.

## Changelog

- 2026-09-12: initial feature (#575). Two condition-gated NPC lines on vanilla's `companion_hire` token; culture-keyed, symmetric, Neutral serves anyone; MCM toggle and scope dropdown.

## GitHub Issue

- **Issue:** #575, [feat(wanderers): wanderers refuse to serve an opposed-alignment player](https://github.com/haterade22/TAOM/issues/575)
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/named-companions.md](./named-companions.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/wanderers-and-named-companions.md](../modding/wanderers-and-named-companions.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
