# Lord Courtesies

## Overview

Two lines under a free lord's main conversation menu. "Gift troops" ends the conversation and opens a donate-style party screen with the lord's party on the left; the troops the player hands over earn relation with him by Bonds of Battle's hand-over rule. "Name the holdings of your house" asks the lord about his clan's settlements: a lord who knows the player well enough names them and they get tracking marks on the map, one who does not refuses, and one whose clan holds nothing says so, each of the last two at a small cost in relation.

No Harmony patch, no GameModel and no save data: one dialog behavior over a service graph. Issue #897.

## Why This Exists

- **Vanilla behavior:** a player can donate troops to a lord of their own kingdom for influence, and has no way to give troops to anyone else. A free lord's holdings are learned by walking there.
- **TAOM requirement:** a Lord of the Rings war runs on favours between rulers. Reinforcing a neighbour's host buys goodwill, and a friend will say where his house keeps its halls. Hard Questions covers the same question for captives by bribe or force; this is the courteous version for lords who are free.
- **Without this feature:** relation with a friendly lord can only be raised by quests and battles, and the map stays dark until the player has travelled it.

## Architecture

### Design Challenge

- The screen opens from the conversation's end one-shot (`ConversationManager.ConversationEndOneShot`, the pattern at `LordConversationsCampaignBehavior.cs:283`), which runs inside `EndConversation`. The party-screen Escape trap does not apply: the relation is written on the engine's commit, and no conversation is open on top of the screen.
- Vanilla's donate screen (`PartyScreenHelper.OpenScreenAsDonateTroops`) pays influence and its done rule is private. The adapter rebuilds the setup with `IsDonating = false`, so vanilla pays no influence, and writes its own done rule. Bonds of Battle's Patch106 pays only from the screen's influence number, which stays 0 on a non-donating screen, so the two never both pay (a wiring test pins both facts).
- The screen edits the lord's live roster in place and reports no list of what moved. The service therefore diffs the roster against the last settled snapshot at each commit.
- `IAlignmentService.AreEnemyAlignments` calls Neutral the enemy of everyone, which would forbid every gift to a Neutral lord. The service uses its own rule instead (see below).

### Solution Approach

`LordCourtesiesService` owns eligibility, the gift flow and the holdings question, and carries the roster diff as the static `Donated`. `ILordCourtesyAdapter` is the engine boundary for the lord and the screen, next to `IArmouryPlayerAdapter` (the player's side and the relation costs) and `IMapRevealAdapter` (the reveal): the lord's cheap snapshot, his member roster, the engine's worth of a gift, the after-conversation hook, the gift screen and the holdings notice. The gift is paid by `TroopHandoverService` (Bonds of Battle), unchanged. `IMapRevealAdapter` (registered by Hard Questions, resolved here) does the reveal. The dialog behavior is thin (ADR-002) and built with `new` for every campaign because it keeps the last domain answer.

### Component Diagram

```
lord_courtesies.json -> LordCourtesiesConfigProvider (validated)
                                |
LordCourtesiesDialogBehavior -> LordCourtesiesService (static Donated: roster diff)
(lines at priority 111)             |
                                    +--> TroopHandoverService (Bonds of Battle: gain, daily cap, grant, notice)
                                    +--> adapters: ILordCourtesyAdapter, IArmouryPlayerAdapter,
                                         IMapRevealAdapter
                                    +--> IAlignmentService, ICoopSessionProvider, IDedicatedServerProvider
```

## What the player sees

Both options sit on `hero_main_options` at priority 111 (vanilla is 100).

- **Gift troops:** the lord answers with a closing line, the conversation ends, and the party screen opens. Only regular troops move, from the player to the lord. Heroes and prisoners stay on the screen but cannot move (the transfer delegate and the prisoner transfer state), troop upgrades are off, and talking from the screen is refused. Done is refused with "That party has no room for so many." while the donated troops would overfill his party. Cancel gives nothing. Every commit pays (Done and map-bar navigation; talking from the screen is refused), and the gift notice is Bonds of Battle's own hand-over line.
- **Holdings:** the lord replies in one of three ways. Willing: each of the clan's towns and castles gets the engine's visual tracking mark, its heroes become known (vanilla prints its "You've learned about" lines), and one line says `{COUNT} holdings marked on your map.` with the number of towns and castles the clan owns (no line for a clan of villages only). Landless: he says his house holds nothing. Unwelcome: he says he does not know the player well enough. The wording differs from Hard Questions' captive line on purpose. The marks cover towns and castles only: a tracked nameplate is drawn on the map every frame, outside the vanilla distance cull and the Nameplate Cull patch, so villages stay unmarked. The player removes a mark with the Track toggle on the settlement's encyclopedia page, and the engine also drops a mark when a siege at that settlement ends, for settlements of the player's faction that were not tracked by hand and while the auto-track setting is below 2 (`SettlementNameplateVM.OnSiegeEventEndedOnSettlement`, `:624-649`). Performance: each mark keeps that nameplate drawn every frame; a kingdom reveal (Hard Questions) marks up to about 27 (the largest kingdom at start), most 4 to 16.

## Eligibility

Both lines need all of these. The matching MCM toggle is checked in `CanGift` and `CanAskDomain`; the co-op and dedicated-server gates are in `Ready`, which `Partner` applies before the single snapshot read; the rest is `LordCourtesiesService.Askable`, a pure check on that snapshot:

- TAOM does not defer to a co-op host (`ICoopSessionProvider.ShouldDeferToHost` is false) and this is not a dedicated server.
- The hero is alive and free (a prisoner never gets either line; Hard Questions owns captives).
- He is not in the player's clan, has a clan, and is not at war with the player's faction.

The gift also needs (`Giftable`):

- He leads his own lord party. A lord idling in a settlement with no party has nothing to receive troops.
- His party is with the player's: in the same settlement as the main party, or in the field within the encounter joining radius with the same map event (none counts), after vanilla's donate menus, with no battle simulation running. Not copied: vanilla's check that the encountered party is not at war with the player; the lord himself is refused at war instead. Engine state cannot tell a messenger conversation from a visit: `MessengerCampaignBehavior` starts a real `PlayerEncounter` and, for a target inside a settlement, moves the main party into that settlement. Messengers therefore owns a flag, `IMessengerConversationState.IsOpen`, set before the encounter starts and cleared when the mission ends, when it fails to open and at session launch; the lord's snapshot reports `IsWithPlayer = false` while it is open. A messenger-reached lord therefore never offers the gift, while the holdings question may still be asked of him.
- His party has room (fewer members than its size limit), is not being disbanded, and is not in a map event other than the player's.
- The player's party holds at least one regular troop.
- He is not in the player's kingdom, where vanilla's donate-for-influence already applies.
- The sides do not cross the line. The rule is `LordCourtesiesService.SidesAllowGift`: a gift is allowed unless one side is Free and the other Evil. Neutral may give or receive. Sides come from `IAlignmentService.ResolveSide` on one `SideIds` per party: the player's `IArmouryPlayerAdapter.Side` (the player clan's map faction with that faction's culture) and the lord's `LordCourtesySnapshot.Side` (`FactionSides.OfHero`: the clan's map faction with that faction's culture, or a clanless hero's own culture). A lord of a Neutral realm is Neutral whatever the lord's own culture (#957). `KingdomId` stays on both for the same-realm gate, which compares kingdom ids only.

## Rules

- **Gift:** the donated troops are the per-character increases between the lord's roster at the last settled reading and the roster at this commit (a decrease is not a donation, heroes are ignored); the reading then advances, so a later commit pays only its new troops. Their value is what vanilla's donate screen would report: the sum of count times `PrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation`, truncated to an int as the screen does (`PartyScreenLogic.cs:834-860`). That number goes to `TroopHandoverService.OnHandover` with the lord, his name and the relation owner, exactly as Patch106 passes them. The rule is [Bonds of Battle's hand-over](bonds-of-battle.md): half the influence, held to 10, a point either way, nothing under 2, at most 10 relation a day per relation owner, and none while enlisted or on a co-op client that defers to the host.
- **Relation owner:** the relation goes through `DiplomacyModel.GetHeroesForEffectiveRelation`: the lord himself with [Personal Relations](personal-relations.md) on (the single-player default), his clan leader when off. The holdings question's costs, charged through `IArmouryPlayerAdapter.ChangeRelationWith`, land on the same pair.
- **Holdings:** the player's relation with the lord (`Hero.GetRelationWithPlayer`) must be at least `revealMinRelation`. Below it: refusal, `refusalCost` relation lost. At or above it and the clan owns no settlement: `landlessCost` relation lost. Otherwise the reveal runs and its count of marked settlements is shown. The refusal check comes first, so an unwelcome landless lord refuses. There is no saved state: the refusal cost itself discourages asking again. An engine fault in the question is logged and the lord gives the refusal reply.
- A cost that rounds to 0 changes no relation.

## Configuration

`Main/_Module/ModuleData/lord_courtesies/lord_courtesies.json`, validated by `LordCourtesiesConfigProvider`. A missing or unparseable file uses the defaults; a value failing its rule reverts to its default with a warning and one summary warning. Cached for the process: an edit needs a full restart. Costs are positive amounts the service subtracts. The file holds the holdings question's values only; the gift has no tunables here because Bonds of Battle owns its rule.

| Field | Default | Rule | Meaning |
|---|---|---|---|
| `revealMinRelation` | 5 | -100 to 100 | Relation needed for an answer |
| `refusalCost` | 1 | 0 to 1,000,000 | Relation lost on a refusal |
| `landlessCost` | 5 | 0 to 1,000,000 | Relation lost asking a landless clan |

### MCM

Group `World/Lord Courtesies` (`GroupOrder = 63`), both default on, neither needs a restart: `EnableTroopGifts` and `EnableDomainReveal`. Both count as simulation-relevant for the co-op settings fingerprint ([coop-interop](coop-interop.md)). The gift is paid by Bonds of Battle's hand-over rule (at most 10 relation a day per relation owner), so the relation needs both Bonds of Battle (`Enable Bonds of Battle`) and its `Troop Gifts Win Lords' Favour` switch on (`TroopHandoverService.OnHandover`'s first gate); with either off the screen still accepts the troops and the lord earns nothing.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LordCourtesies/LordCourtesiesService.cs` | Eligibility, the gift flow, the holdings question and the static roster diff `Donated` |
| `Main/Features/LordCourtesies/LordCourtesiesConfigProvider.cs` | Validated JSON config |
| `Main/Features/LordCourtesies/LordCourtesiesModule.cs` | Feature module (one line in `Main/Composition/FeatureModules.cs`) |
| `Main/Features/LordCourtesies/LordCourtesiesIoC.cs` | DryIoc registration |
| `Main/Features/LordCourtesies/Hooks/LordCourtesiesDialogBehavior.cs` | Thin campaign behavior |
| `Main/Features/LordCourtesies/Hooks/LordCourtesiesDialogLines.cs` | The conversation lines |
| `Main/Adapters/ILordCourtesyAdapter.cs`, `LordCourtesyAdapter.cs` | Engine boundary: snapshot, roster, gift screen, engine worth |
| `Main/Adapters/MapRevealAdapter.cs` | Shared with Hard Questions and, through the by-id pair `FortificationsAround` and `Track`, [Notable Services](notable-services.md) and [Inn Rounds](inn-rounds.md) (both through `HoldsRevealService`, which also reads `MapDiagonal` and `ShowHoldsMarkedNotice`): tracking marks and known heroes |
| `Main/_Module/ModuleData/lord_courtesies/lord_courtesies.json` | Config |

## Dependencies

- `TroopHandoverService` (Bonds of Battle): the gain, the daily cap, the grant and the notice. Unchanged; this feature only calls `OnHandover`.
- `IAlignmentService` (Execution): sides of the player and the lord.
- `IArmouryPlayerAdapter` (Adapters): the player's kingdom id (the same-realm gate), the player's `Side` pair, and `ChangeRelationWith` for the holdings costs.
- `IMapRevealAdapter` (Adapters): `RevealClan`. Notable Services and Inn Rounds are further consumers, through `HoldsRevealService`, which reads `FortificationsAround`, `Track`, `MapDiagonal` and `ShowHoldsMarkedNotice`. Registered by `HardQuestionsIoC`; this feature resolves it and does not register it again.
- `ICoopSessionProvider` and `IDedicatedServerProvider` (CoopInterop): hide both lines when TAOM defers to a host and on a dedicated server.
- `PartyScreenHelper.DonateModeTroopTransferableDelegate` (engine, public): troops move from the player's side only and heroes never move.

## Tests

`TAOM.Tests/Features/LordCourtesies/`: `DonatedTroopsTests` (the roster diff), `LordCourtesiesBehaviorTests` (the thin dialog behavior and the four reply routes; needs the engine assemblies), `LordCourtesiesServiceTests` (every eligibility rule including the side matrix and co-location, the gift flow with the roster snapshot ordering, the hand-over payment, repeated commits and the shared daily cap, each domain outcome, one snapshot read per check, the reply routing), `LordCourtesiesConfigProviderTests` (one test per rule, the shipped file equals the defaults) and `LordCourtesiesWiringTests` (module listed once, nothing in `IoC.cs` or `SubModule.cs`, behavior built with `new`, MCM group, the screen is not donating, settles on the engine's commit, tracker marks, the neutral no-room hint in every language file).

## Limits

On a co-op host the installed co-op module replaces `DoneLogic` with its own prefix (`docs/reference/harmony-patch-registry.md`, Patch106 entry), so a gift there may move troops without paying, as for Bonds of Battle's own hand-over. The service also pays nothing while `ShouldDeferToHost`.

## Dropped from the proposal

Four parts of the original proposal are not built. AI lords selling bandit prisoners: vanilla's `PartiesSellPrisonerCampaignBehavior` already does it. Bandit conversion: almost no bandit-culture troops ever reach a lord, and vanilla never recruits them, so there was nothing to convert. The granary: vanilla lords already buy food in towns and villages, and TAOM's AI Food Relief exists. The culture feud and the ruler tick were skipped by the spec.

## Translations

The 10 new keys carry translated text in all 12 languages (2026-11-12, #763).

## In-game checklist

- Talk to a free lord of another kingdom who leads a party and stands beside you (same town, or the same field encounter): the gift line appears. It does not for your own clan, your own kingdom, a lord at war with you, a lord across the Free/Evil line, a prisoner, a lord whose party is full or disbanding, or when you carry no regular troops.
- Gift in a town, in an army, and try it by messenger, including to a lord inside a settlement: the first two offer the line, the messenger conversation does not (it still offers the holdings question).
- Choose it: the conversation ends, then the party screen opens with his party on the left. Troops move right to left only; heroes and prisoners do not move; talking is refused; Done is refused with the "no room" hint when his party would overflow; Cancel gives nothing.
- Done pays once with Bonds of Battle's hand-over line; a second gift to the same lord the same day is capped by the daily rule (10 relation per relation owner).
- Move troops, then leave by the map bar instead of Done: the gift still pays.
- Ask the holdings question at relation 5 or more: the clan's towns and castles show tracking marks and the count line shows; open one settlement's encyclopedia page and switch Track off to remove its mark. Below 5: refusal and 1 relation lost. A landless clan at relation 5 or more: 5 relation lost.
- Toggle each MCM option off: its line disappears and the other stays.
- Neutral realm (owed, #957): as a player in a Neutral realm (Khand, Umbar, Shaghana or Abanissa), or with a Gondor-born lord of a Neutral realm beside you, the gift line is offered across the Free/Evil line: a Neutral player to an Evil lord and to a Free lord, and a Free player to a lord of a Neutral realm whatever the lord's own culture.
- Reveal, save and load: the marks are still on the map after loading.
- In German, the new lines show English text.

## Changelog

- 2026-10-20 (#957, part 2): the sides are read from `SideIds` (the realm's id with the realm's culture), not the realm id with the hero's own culture; `LordCourtesySnapshot.CultureId` is replaced by `Side`.
- 2026-10-20: the reveal marks towns and castles only; the roster diff is a static on the service; the gift reward reuses Bonds of Battle's hand-over rule and settles on the engine's commit; the reveal marks settlements with the visual tracker (also for Hard Questions); co-location, room, disbanding and troop checks added to the gift line.
- 2026-10-19: first version (#897).

## GitHub Issue

- **Issue:** #897, Lord Courtesies: [issue #897](https://github.com/haterade22/TAOM/issues/897)
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bonds-of-battle.md](./bonds-of-battle.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/messengers.md](./messengers.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
