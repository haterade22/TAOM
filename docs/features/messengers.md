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
- Pre-flight validation has 12 rejection paths (dead, prisoner, fugitive, child, in player party, same settlement, same army, not enough gold, etc.). Putting all of those in the campaign behavior would explode the boundary class. Validation lives in the service via a `HeroSnapshot` POCO that `MessengerTargetReader.ReadSnapshot` fills at the boundary.
- Bannerlord 1.3.15 introduced API breaks vs LOTRAOM's 1.2.12: `IMissionListener.OnInitialDeploymentPlanMade(BattleSideEnum, bool)` removed → replaced by `OnDeploymentPlanMade(Team, bool)`; `TextObject.Empty` removed → `TextObject.GetEmpty()`; `MobileParty.Position2D` setter removed → `MobileParty.Position = new CampaignVec2(vec, isOnLand)`; `IMapPoint.Position2D` (Vec2) renamed to `IMapPoint.Position` (CampaignVec2 — needs `.ToVec2()`); `CampaignTime` ctor became internal — use `CampaignTime.Now.ToDays` for elapsed math.

### Solution Approach

Layered TAOM architecture. The behavior is the TaleWorlds-bound boundary; the service is pure logic; the store owns mutable state; the config provider validates JSON tunables; the settings provider wraps MCM.

- **`MessengerCampaignBehavior`** (entry point): registers events (`HourlyTickEvent`, `OnSessionLaunchedEvent`), implements `IMissionListener`, registers the dialog tree, orchestrates encounter routing (settlement vs field), and serialises state via `SyncData`. Touches sealed types directly because that's what the boundary is for.
- **`MessengerService`** (Reuse.Singleton): pure logic: `CanSendMessenger(HeroSnapshot?, playerGold, quotedCost, inPerson) → MessengerValidationResult`, `GetOffer(HeroSnapshot?, MessengerQuoteInput, playerGold, inPerson) → MessengerOffer` (quote, verdict against that quote, days), `ClassifyArrival(HeroSnapshot, sharesMainHeroParty, playerAvailable, targetPartyInMapEvent) → MessengerArrivalVerdict` (lost, confused, wait or ready for an arrived messenger; the hourly tick switches on it), `QuoteCost(MessengerQuoteInput) → int`, `QuoteTravelDays(MessengerQuoteInput) → int`, `RollAccident()`, `AdvancePosition(current, target, speed) → PositionUpdate`, `CalculateMessengerSpeed(mapDiagonal, travelDays)`. Tested against NSubstitute mocks.
- **`MessengerConversationState`** (Reuse.Singleton, `IMessengerConversationState`): one in-memory flag, `IsOpen`, set just before the encounter starts, cleared in `OnEndMission`, on the null-mission recovery, when opening throws (`AbortConversation`) and at session launch. The conversation looks like a real visit to the engine (a real `PlayerEncounter`, and the main party inside the target's settlement), so other features read this flag to tell them apart: [Lord Courtesies](lord-courtesies.md) hides its troop gift while it is open and still allows its holdings question, Enlistment greys the oath, Tokens of Esteem hides its gift line, [Notable Services](notable-services.md) hides all three of its lines, and Patch117 hides six vanilla lines.
- **`MessengerStateStore`** — owns the in-memory `Dictionary<heroId, PendingMessenger>`, exposes CRUD + `Serialize`/`Deserialize` for the behavior's `SyncData`.
- **`MessengerConfigProvider`** — validates `messenger_config.json` per the "Config Providers MUST Validate" rule (range-check + revert + warn).
- **`MessengerSettingsProvider`** — wraps `TaomSettings.Instance.*` so the service can be mocked in tests.
- **`MessengerEncyclopediaPrefabExtension` + `MessengerEncyclopediaMixin`**: UIExtenderEx integration. The prefab extension appends a `<ListPanel>` containing a "Send Messenger" `<ButtonWidget>` after the `RichTextWidget[@Text='@InformationText']` in `EncyclopediaHeroPage`. The mixin exposes `IsMessengerAvailable`, `SendMessengerHint` and `SendMessengerActionName` as data sources (the action name carries the price: "Send Messenger (N denars)"), and `ExecuteSendMessenger` as the click command. It reads one `MessengerOffer` from `MessengerCampaignBehavior.GetOffer(hero, inPerson: false)`, which delegates to the service, the same quote the dialog and the send use.

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
        +-> calls MessengerCampaignBehavior.GetOffer(hero, inPerson: false): verdict, quoted price, days (engine reads in MessengerTargetReader)
        +-> calls Campaign.Current.GetCampaignBehavior<MessengerCampaignBehavior>().SendMessenger(hero, inPerson: false) on click
```

## Configuration

### MCM (player-facing) — [TaomSettings.cs](../../Main/Features/TaomSettings.cs)

Group: **Messengers**

| Setting | Type | Range | Default | Effect |
|---------|------|-------|---------|--------|
| `EnableMessengers` | bool | n/a | true | Master toggle, read at each use (`RequireRestart = false`). When off, the dialog line, the send and the hourly delivery stop and the encyclopedia button is greyed out, and hovering it shows why. |
| `MessengerGoldCost` | int | 10-500 | 50 | Shown as "Base Gold Cost". The price of the shortest trip; the distance and the three multipliers below build on it. The property id and default are unchanged (a persisted MCM value keeps working). |
| `MessengerTravelDays` | int | 1-10 | 3 | In-game days a messenger spends in transit. Speed scales to map size: `mapDiagonal / (24 * travelDays) * speedMultiplier`. The same days also scale the price (a longer crossing costs more per map distance). |
| `MessengerAccidents` | bool | — | true | Master toggle for the random ambush roll. |

### JSON config — [messenger_config.json](../../Main/_Module/ModuleData/messengers/messenger_config.json)

Advanced tunables that are not exposed in MCM.

```json
{
  "accidentChancePerHour": 0.002,
  "travelSpeedMultiplier": 1.0,
  "goldPerTravelDay": 25.0,
  "atWarMultiplier": 2.0,
  "neverMetMultiplier": 2.0,
  "besiegedMultiplier": 5.0
}
```

| Field | Range | Default | Effect |
|-------|-------|---------|--------|
| `accidentChancePerHour` | [0.0, 1.0] | 0.002 | Per-hour ambush probability (gated by MCM `MessengerAccidents`). 0.002 = ~0.2% per hour, ~5% chance over a 24-hour leg. |
| `travelSpeedMultiplier` | [0.1, 10.0] | 1.0 | Multiplies the per-hour travel speed. Use to retune travel without changing in-game `TravelDays`. It also scales the price: a faster messenger spends fewer days on the same distance. |
| `goldPerTravelDay` | [0, 1000] | 25 | Denars added per in-game day of the trip. |
| `atWarMultiplier` | [1, 10] | 2 | Price multiplier when the target is at war with you. |
| `neverMetMultiplier` | [1, 10] | 2 | Price multiplier when you have never met the target. |
| `besiegedMultiplier` | [1, 10] | 5 | Price multiplier when the target sits in a besieged settlement. |

Out-of-range values revert to defaults with a warning. **JSON config requires a Bannerlord application restart to reload** — `MessengerConfigProvider` is `Reuse.Singleton` and caches the config at first access for the entire process lifetime.

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/Messengers/IMessengerService.cs](../../Main/Features/Messengers/IMessengerService.cs) | Service contract (validation, price quote, accident roll, position math, speed calc) |
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
| [Main/Features/Messengers/Domain/HeroSnapshot.cs](../../Main/Features/Messengers/Domain/HeroSnapshot.cs) | Boundary POCO (12 hero properties, the engine hero state among them) for the send check and the arrival verdict |
| [Main/Features/Messengers/Domain/MessengerArrivalVerdict.cs](../../Main/Features/Messengers/Domain/MessengerArrivalVerdict.cs) | 4-value enum: what the hourly tick does with an arrived messenger |
| [Main/Features/Messengers/Domain/MessengerHeroState.cs](../../Main/Features/Messengers/Domain/MessengerHeroState.cs) | The engine's eight hero states, one to one, read by MessengerTargetReader |
| [Main/Features/Messengers/Domain/MessengerValidationResult.cs](../../Main/Features/Messengers/Domain/MessengerValidationResult.cs) | 13-value enum: Ok plus 12 rejections |
| [Main/Features/Messengers/Domain/PendingMessenger.cs](../../Main/Features/Messengers/Domain/PendingMessenger.cs) | Saveable POCO + `Serialize`/`TryDeserialize` |
| [Main/Features/Messengers/Domain/PositionUpdate.cs](../../Main/Features/Messengers/Domain/PositionUpdate.cs) | Service return struct (new position + arrived flag) |
| [Main/Features/Messengers/Domain/MapCoord.cs](../../Main/Features/Messengers/Domain/MapCoord.cs) | TAOM-owned 2D map-coordinate struct — keeps the service/domain layers free of TaleWorlds types per ADR-007; the boundary (`MessengerCampaignBehavior`) converts to/from `Vec2` (NaN = invalid sentinel, `IsValid` matches `Vec2.IsValid` incl. ±Infinity rejection) |
| [Main/Features/Messengers/MessengerCampaignBehavior.cs](../../Main/Features/Messengers/MessengerCampaignBehavior.cs) | Boundary: dialog tree, IMissionListener, encounter routing, `SyncData` |
| [Main/Features/Messengers/MessengerTargetReader.cs](../../Main/Features/Messengers/MessengerTargetReader.cs) | The one engine read of a target: validation snapshot and quote input (#898) |
| [Main/Features/Messengers/Domain/MessengerOffer.cs](../../Main/Features/Messengers/Domain/MessengerOffer.cs) | Verdict, quoted price and days for one target |
| [Main/Features/Messengers/Domain/MessengerQuoteInput.cs](../../Main/Features/Messengers/Domain/MessengerQuoteInput.cs) | What the price depends on |
| [Main/Features/Messengers/MessengerEntryPolicy.cs](../../Main/Features/Messengers/MessengerEntryPolicy.cs) | Pure rule: when entering the town would attach the player to an army (#898) |
| [Main/Features/Messengers/MessengerSessionRulesModule.cs](../../Main/Features/Messengers/MessengerSessionRulesModule.cs) | Applies Patch117 at GameInit |
| [Main/Features/Messengers/Hooks/MessengerSessionRulesPatch.cs](../../Main/Features/Messengers/Hooks/MessengerSessionRulesPatch.cs) | Patch117 postfix over six vanilla conditions |
| [Main/Features/Messengers/MessengerIoC.cs](../../Main/Features/Messengers/MessengerIoC.cs) | DryIoc registrations |
| [Client/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs](../../Client/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs) | UIExtenderEx prefab patch (button injection) |
| [Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs](../../Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs) | UIExtenderEx VM mixin (data sources + click command) |
| [Main/_Module/ModuleData/messengers/messenger_config.json](../../Main/_Module/ModuleData/messengers/messenger_config.json) | Advanced tuning |
| [Main/_Module/ModuleData/taom_messenger_strings.xml](../../Main/_Module/ModuleData/taom_messenger_strings.xml) | EN base. Keys added since #898 live in `taom_module_strings.xml` (harvested by `tools/harvest_literal_loc_keys.py`) |
| `Main/_Module/ModuleData/Languages/<LANG>/std_taom_messenger_strings_*.xml` | 12 language variants (BR/CNs/CNt/DE/FR/IT/JP/KO/PL/RU/SP/TR), translated (2026-11-12, #763). The five keys added by #898 (`taom_enlist_not_by_messenger`, `taom_send_messenger_cost`, `taom_messenger_trip_days`, `taom_messenger_same_settlement`, `taom_messenger_same_army`) are in `taom_module_strings.xml` and its language files; they are translated too (#763). |

## Pricing and refusals (#898)

`MessengerService.QuoteCost` is pure. With `days = travelDays * clamp01(distance / (mapDiagonal * travelSpeedMultiplier))`:

```
cost = ceil((base + goldPerTravelDay * days) * (atWar ? atWarMultiplier : 1) * (neverMet ? neverMetMultiplier : 1) * (besieged ? besiegedMultiplier : 1))
```

The result is clamped to the base price at the low end and 100000 at the top. A NaN or negative distance (a target with no map position, such as an unspawned wanderer) and a map diagonal that is zero, negative or NaN count as a full trip. Config values that are not finite or are out of range revert to the defaults in the provider, with a warning; the service trusts the provider and keeps a NaN exit gate on the computed price (a non-finite result is the base price). The price is paid when the messenger leaves and never refunded, so it is not stored and the save format is unchanged.

`MessengerTargetReader` is the one engine read: the validation snapshot and the quote input (distance between the two map points, `Campaign.MapDiagonal`, `FactionManager.IsAtWarAgainstFaction` (false for a null faction), `!Hero.HasMet`, `Settlement.IsUnderSiege`, and for the two "together" refusals `Army.DoesLeaderPartyAndAttachedPartiesContain`: both parties must be in the leader's group, so a lord still marching to join is not "in your army"). `MessengerCampaignBehavior.GetOffer` turns it into a `MessengerOffer` (verdict, price, days) for the dialog, the send and the encyclopedia button. A stranger is a surcharge, not a refusal.

Refusals added to the validation ladder, after "in your party" and before the gold check: `SameSettlement` (the target is in the settlement the player is in) and `SameArmy` (the target is in the player's army). Each has a reason line the greyed button shows. The gold check compares against the quoted price, and "Not enough gold" names that price. The button reads "Send Messenger (N denars)"; the enabled hint says the messenger will take about N days ("1 day" when it is one), in a second hint inside the button. The two "together" refusals apply only to the remote send from the encyclopedia: the dialog line keeps its old behaviour, so a lord in the same hall is still offered the messenger line (`GetOffer(hero, inPerson: true)`). The "arrives within N days" notice still names the travel-days cap, since the straight-line estimate is not a promise.

## What a messenger conversation cannot do (#898)

The conversation is a real `PlayerEncounter` with the messenger, and for a target inside a settlement the main party is moved there, so the engine cannot tell it from a visit. These things would break or be wrong if offered, and each is closed:

- **"I want to join your army" and "I want you to join my army"** are hidden. Their consequences attach a party to an army (`Army.AddPartyToMergedParties` sets `AttachedTo`), which snaps it across the map. Patch117.
- **Board games** are hidden: the game starts after the window closes, when the messenger has already ended the mission. Patch117.
- **Enlisting** stays visible but greyed, with "Swear service in person, not through a messenger." Swearing finishes the encounter and parks the player at the commander, a teleport. `EnlistmentDialogBehavior.OfferIsTakeable(verdict, throughMessenger)`.
- **Clan member troop and inventory exchange** (`clan_member_manage_troops`, `clan_member_manage_inventory`) are hidden: they open the party screens for a person who is not there. Patch117.
- **Companion recall** (`companion_rejoin_after_emprisonment_role`) is hidden for the same reason. Patch117.
- **The Tokens of Esteem gift line** is hidden (`GiftConversationFlow.CanOffer`): goods cannot be handed across the map, and a messenger only reaches a lord, whose list holds goods alone.
- **Lord Courtesies' troop gift** reads the flag (`LordCourtesyAdapter`) and is not offered.
- **A main party in an army it does not lead, with that army's leader in the target's town**: entering the town would make `EnterSettlementAction.ApplyForParty` attach the party, and `PlayerEncounter.Finish` never leaves the town for an attached party. That state opens the field conversation instead (`MessengerEntryPolicy.WouldAttachToArmy`).

The flag is `IMessengerConversationState.IsOpen`: set when the arrived messenger's conversation starts, cleared when its mission ends, when the mission fails to open, when opening throws (`AbortConversation`, which also schedules the encounter cleanup), and at each session launch. Lord Courtesies, Enlistment, Tokens of Esteem, Notable Services and Patch117 read it.

Wanderer hiring and barter stay on purpose: remote hiring is intended, and a trade settles by gold. The army "gathering" answer is unreachable in v1.5.5 (no line outputs `lord_tell_gathering`); its consequence would only set `MainParty.Army`. Registry: [Patch117_MessengerSessionRules](../reference/harmony-patch-registry.md).

## In-game Checklist (#898)

1. **Army join, both ways.** Talk to a lord through a messenger: neither "I want to join your army" nor "I want you to join my army" shows.
2. **Enlist.** "I wish to enlist under your command." shows greyed with "Swear service in person, not through a messenger."
3. **Board game.** No board game line in a lord's hall conversation through a messenger.
4. **Clan member.** A clan member who leads a party: no "Let me inspect your troops." and no "Let me inspect your equipment."
5. **Companion recall.** A companion in a town: no recall line.
6. **Gift.** No "I have a gift for you." line (Tokens of Esteem).
7. **Troop gift.** No Lord Courtesies troop gift line.
8. **In an army, not attached.** Join a lord's army, stay unattached, send a messenger to a lord in a town where the army's leader is: the field conversation opens and the party does not attach.
9. **The lord's hall.** A lord in a town and a lady in her home castle: the conversation opens in the hall, ends cleanly, and the party is back where it was.
10. **Near lord.** A lord in the next town: the button shows about the base price, the hint says about 1 day.
11. **Far lord.** A lord across the map: a higher price and a longer hint. 125 appears only corner to corner; a far lord shows less.
12. **At war.** A lord of a kingdom at war with you: the price doubles.
13. **Never met.** A hero you have not talked to: the price doubles and the button is still enabled.
14. **Besieged.** A lord inside a besieged town: the price is five times.
15. **Same settlement.** Stand in a town with a lord and open their page: the button is greyed with the reason line.
16. **Same army.** Join a lord's army and open another member's page: greyed with the reason line.
17. **Poor.** With less gold than the price: greyed, and the hint names the price.
18. **Days hint.** Hover the enabled button: the hint says about N days, and "about 1 day" (singular) for a lord next door.
19. **Still marching.** A lord who was called to your army but is still marching is not refused as "in your army".
20. **Dispatch line in the hall.** The dispatch line still shows in a lord's hall conversation.
21. **In person.** Checks 1 to 7 above are for the messenger; face to face, those lines are all still there.
22. **In-person price.** In a lord's hall the dispatch line still shows, and its price still carries the war and siege surcharges. The design keeps this on purpose.

## Dependencies

- `IMessengerSettingsProvider` — wraps `TaomSettings.Instance` for the 4 MCM toggles
- `IMessengerConfigProvider` — wraps `messenger_config.json`
- `IMessengerStateStore` — owns the pending-messenger collection
- `IMessengerRandomSource` — wraps `MBRandom.RandomFloat`
- `IModLogger` (Core) — used by `MessengerConfigProvider` and `MessengerStateStore` for warnings
- `IPathService` (Core) — used by `MessengerConfigProvider` to locate `messenger_config.json`
- TaleWorlds (boundary only): `Hero`, `MobileParty`, `Settlement`, `PartyBase`, `PlayerEncounter`, `LocationComplex`, `Mission`, `IMissionListener`, `ConversationCharacterData`, `CampaignTime`, `CampaignVec2`, `Vec2`, `EnterSettlementAction`, `GiveGoldAction`, `CampaignEvents`, `CampaignEventDispatcher`, `MBTextManager`, `InformationManager`, `FactionManager`, `Army` (the last two read in `MessengerTargetReader`)

## Tests

Unit tests:

- [TAOM.Tests/Features/Messengers/MessengerServiceTests.cs](../../TAOM.Tests/Features/Messengers/MessengerServiceTests.cs): every `MessengerValidationResult` rejection path, the price quote (distance, multipliers, ceiling, bad inputs), accident roll boundaries (p=0/1/disabled), position math edge cases (distance < / = / > speed; invalid target), speed calculation (zero/negative days clamping, multiplier).
- [TAOM.Tests/Features/Messengers/MessengerArrivalClassificationTests.cs](../../TAOM.Tests/Features/Messengers/MessengerArrivalClassificationTests.cs): the arrival verdict over every hero state, wanderer or not, child or not, shared party, battle and player availability, checked against the rule as the behavior ran it; a prisoner is lost on arrival; the enum matches the engine's states.
- [TAOM.Tests/Features/Messengers/MessengerStateStoreTests.cs](../../TAOM.Tests/Features/Messengers/MessengerStateStoreTests.cs): Add/Remove/Get/Contains/Clear, duplicate-id replacement, null/empty input, save roundtrip preserves all fields, malformed-on-load drops + warns.
- [TAOM.Tests/Features/Messengers/MessengerConfigProviderTests.cs](../../TAOM.Tests/Features/Messengers/MessengerConfigProviderTests.cs): valid JSON, missing file, malformed JSON, every validation rule (negative chance, >1.0 chance, zero/negative speed mult, absurd speed mult, the four pricing fields out of range and non-finite), boundary values, partial JSON merge, caching, info-vs-warning logging.
- [TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs](../../TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs): the button's shape (#944). The button sits in an always-enabled wrapper whose hint shows the reason for a greyed button, and the button holds a second hint for the enabled state (the days), because an enabled button takes the hover.
- [TAOM.Tests/Features/Messengers/MessengerShippedConfigTests.cs](../../TAOM.Tests/Features/Messengers/MessengerShippedConfigTests.cs): the shipped `messenger_config.json` equals the defaults and names every field.
- [TAOM.Tests/Features/Messengers/MessengerSessionRulesPatchTests.cs](../../TAOM.Tests/Features/Messengers/MessengerSessionRulesPatchTests.cs): the postfix filter, the six targets and the JIT check against the installed DLLs (#898).
- [TAOM.Tests/Features/Messengers/MessengerSessionRulesWiringTests.cs](../../TAOM.Tests/Features/Messengers/MessengerSessionRulesWiringTests.cs): one module line, Patch117 at GameInit, nothing registered by hand in `IoC.cs`.
- [TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs](../../TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs): source pins on the recovery when opening throws or returns no mission, the cleanup `AbortConversation` schedules, the one offer behind the dialog, the send and the button, the in-person dialog path, the quote reads (war, stranger, siege) and the mixin's remote send.
- [TAOM.Tests/Features/Messengers/MessengerEntryPolicyTests.cs](../../TAOM.Tests/Features/Messengers/MessengerEntryPolicyTests.cs): the army-attach rule and the source pin on the settlement branch.
- [TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs](../../TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs): the registration in the IoC and SubModule, the behavior's dependencies, and the main-party guard in `IsPlayerAvailable`.
- [TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs](../../TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs): the open-conversation flag, where the behavior opens it and every path that closes it.

The boundary classes (`MessengerCampaignBehavior`, the UI mixin, `MessengerTargetReader`, `MessengerSettingsProvider`, `MessengerRandomSource`, `MessengerIoC`) follow TAOM's "boundaries are not unit-tested" convention and are validated through manual game-testing (see GitHub issue #109 manual test plan).

## How to add a new validation rejection reason

1. Add the new value to `MessengerValidationResult` (e.g., `HeroRetired`).
2. Add a corresponding skip-guard in `MessengerService.CanSendMessenger` returning the new value.
3. Add a `case` to `MessengerCampaignBehavior.GetReason(MessengerOffer, Hero)` that returns the player-facing `TextObject` with `{HERO_NAME}` substitution if needed.
4. Add the localization key (`taom_messenger_<reason>`) as an inline `{=key}English` literal, then run `tools/harvest_literal_loc_keys.py --apply` and the seed-only translate step for the 12 languages (new keys land in `taom_module_strings.xml`).
5. Add a unit test in `MessengerServiceTests` named `CanSendMessenger_<NewState>_Returns<NewEnum>`.
6. Optionally extend `HeroSnapshot` (and `MessengerTargetReader.ReadSnapshot`, which fills it) if the new check needs a property not already exposed.

## How to add a new advanced tuning knob to messenger_config.json

1. Add the property to `MessengerConfig.cs` with its default value.
2. Add one `Checked(key, parsed.Value, min, max, defaults.Value, ref rejected)` member to the initializer in `MessengerConfigProvider.Validate`. It rejects NaN, infinity and out-of-range values, logs a warning and reverts to the default.
3. Consume the new property in `MessengerService` (or wherever the consumer lives).
4. Add a unit test in `MessengerConfigProviderTests` named `GetConfig_<InvalidVariant>_RevertsToTheDefaultAndWarns` for each validation rule, plus a boundary test.
5. Document the new field + range in the "JSON config" table above.

## Performance

- `OnHourlyTick` uses a reusable `_toRemoveScratch` field (cleared per tick) instead of allocating `new List<string>()` each campaign hour.
- `IMessengerStateStore.GetAll()` returns the live `Dictionary<,>.ValueCollection` (zero allocation).
- `MessengerEncyclopediaMixin` builds the four fixed `HintViewModel` instances once (`_hintTargetUnavailable`, `_hintMessengersDisabled`, `_hintSystemUnavailable` and the empty initial one) and builds the label and the reason or days hint on every refresh; the page refreshes when it opens and after a click, never per frame.
- `MessengerConfigProvider` and `MessengerSettingsProvider` are `Reuse.Singleton`; config is loaded once via `Lazy<>` at first access.

## Changelog

- 2026-10-23: the price grows with the distance to the target and is higher for a war, a stranger and a besieged town; the button shows it and the hint shows about how many days; a target in the same settlement or army is refused with a reason (#898). The base price keeps its MCM id and default and is shown as "Base Gold Cost". Docs: the encyclopedia mixin has no `SendMessengerCost` data source, and `EnableMessengers` needs no restart.
- 2026-10-23: a messenger conversation no longer offers the join-army lines, the board game, the clan troop and inventory exchange or the companion recall (Patch117); the enlist oath is greyed and the gift line is hidden there; a player in an army whose leader is in the target town gets the field conversation; a throw while opening recovers and rethrows (#898).
- 2026-10-24: the Send Messenger button now uses the declared brushes `Popup.Button.Base` and `Popup.Button.Text` (its two old names resolved to nothing), and its label dims when the button is disabled (#989).
- 2026-10-20: the Send Messenger hint for the disabled state now shows: the hint sits beside the button in an enabled wrapper, as vanilla's governor button does (#944).
- 2026-10-20: added `IMessengerConversationState`, the open-conversation flag Lord Courtesies reads.
- 2026-05-13 — Wired Messengers into IoC + campaign starter (fixed encyclopedia hero-click NRE, #121) and added a wiring regression test (#191); fixed UI mixin notifications firing on self (#166) and per-campaign/arrival state-reset gaps (#123).
- 2026-05-06 — Ported the LOTRAOM messenger system to TAOM (1.3.15): paid messenger dispatch from the encyclopedia/dialog, travel + arrival conversation routing, primitive-dict save, MCM + JSON tunables, 12-language localization (#109).

## GitHub Issue

- **Issue:** [#898 (messenger refinements)](https://github.com/haterade22/TAOM/issues/898)
- **Status:** Closed; in-game checks owed (label `triage-needs-ingame`, checklist above)

- **Issue:** [#109 — feat(messengers): port LOTRAOM messenger system to TAOM (1.3.15)](https://github.com/haterade22/TAOM/issues/109)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/enlistment.md](./enlistment.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
