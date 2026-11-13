# Hard Questions

## Overview

A lord held prisoner by the player's clan can be questioned about his lands and stripped of his kit. Talking to such a lord adds up to two options under his main conversation menu. "Ask about his lands" has a clan topic and a kingdom topic: he refuses, and the player either pays a bribe or uses force. Either way the matching settlements get the engine's visual tracking mark on the map (towns and castles) and unknown lords become known. "Strip him" takes his weapons, armour, horse and harness into the player's inventory and leaves him in rags. Force and stripping cost relation, Honor, Mercy and (for the strip) clan renown.

No Harmony patch and no GameModel: two campaign behaviors over a service graph. Issue #896.

## Why This Exists

- **Vanilla behavior:** a captive lord offers only ransom, release and recruitment. Nothing lets the player pry information out of him or take his gear.
- **TAOM requirement:** a Lord of the Rings war has captains wringing dispositions from captives and leaving them in rags. The relation costs follow the Free/Evil alignment rule the Execution feature already applies to kinslaying.
- **Without this feature:** the player has no way to learn a rival realm's holdings except by walking there.

## Architecture

### Design Challenge

- A wound, a death or a change of kit must not happen under the lord's own conversation, so force and strip run at `CampaignEvents.ConversationEnded`.
- `Hero.BattleEquipment`, `CivilianEquipment` and `StealthEquipment` fall back to one object shared by every hero when the hero holds no copy of his own (`Hero.cs:221-225`). Writing to it would change every such hero.
- The captor clan of a prisoner is a party's owner clan or a dungeon settlement's owner clan, and `PartyBase.Owner` is banned in TAOM (`Settlement.Owner` is `OwnerClan.Leader` and throws without a clan).

### Solution Approach

`InterrogationService` owns eligibility, the bribe price, the bribe itself and a one-slot queue for a forced question or a strip. `StripService` strips a lord and re-kits him on release. `HardQuestionsPenaltyApplier` is shared by both: relation changes through `IExecutionRelationService`, trait experience scaled by side, companion judgement, and the one-line relation summary. The dialog behavior and the campaign behavior are thin (ADR-002) and are built with `new` for every campaign, because each resets per-campaign state in its constructor (a container singleton is constructed once per process).

### Component Diagram

```
hard_questions.json -> HardQuestionsConfigProvider (validated)
                                |
HardQuestionsDialogBehavior -> InterrogationService -> StripService
(lines at priority 111)          |        |                |
                                 |   HardQuestionsPenaltyApplier -> IAlignmentService,
HardQuestionsCampaignBehavior    |                                  IExecutionRelationService
(SyncData, release)              +--> HardQuestionsState (saved)
                                 +--> adapters: ICaptiveLordAdapter, IHardQuestionsActionAdapter,
                                      IPlayerTraitAdapter, IMapRevealAdapter, ILordEquipmentAdapter
```

## What the player sees

The options sit on `hero_main_options` at priority 111 (vanilla is 100). Asking about lands leads to the lord's refusal, then a choice: pay a price, force him, or withdraw. The price line is greyed with an explanation when the player cannot afford it. Force ends the conversation with a closing line from the lord. A bribe gets a reply that depends on the lord: Calculating first, then Valor, otherwise a plain one, and the conversation returns to the lord's menu. Strip asks for confirmation first, then ends the conversation.

## Eligibility

All of these must hold (`InterrogationService.Eligible`; the used-topic check runs first because it is a dictionary lookup):

- The MCM toggle is on.
- TAOM does not have to defer to a co-op host (`ICoopSessionProvider.ShouldDeferToHost` is false). The options are hidden whenever TAOM must defer, including under a co-op mod whose role TAOM cannot read.
- The conversation is not one of the prisoner-decision contexts (`CapturedLord`, `FreeOrCapturePrisonerHero`).
- No ransom screen is open (`ICaptiveLordAdapter.IsRansomScreenOpen`: the top `PartyState` is in `PartyScreenMode.Ransom`). That screen holds cloned rosters and never writes them back, a lord killed while it is open stays on it, and selling him there ransoms him back to life and pays for him; the strip and the bribe are hidden too, so one rule covers every line (#1001). The normal party screen binds the live rosters and keeps the lines.
- The hero is a living prisoner whose captor is a party or a settlement of the player's clan, and he is not in the player's clan.
- The topic is unused for this captivity, and for the kingdom topic the lord has a kingdom.

## The two topics

- **Bribe price:** the lord's ransom (`RansomValueCalculationModel.PrisonerRansomValue`, seller the player) times 1.5 for the clan topic or 3.5 for the kingdom topic, times 0.5 for a Calculating lord. Rounded, at least 1, capped at `int.MaxValue / 2`. The gold goes to the lord. The player gains 50 (clan) or 90 (kingdom) Charm experience.
- **Reveal scope:** the clan topic puts a visual tracking mark on each of the clan's towns and castles and makes its heroes known; the kingdom topic does the same for the kingdom. The marks are the engine's own (`VisualTrackerManager.RegisterObject`, saved with the campaign); a settlement already tracked is left alone. The marks cover towns and castles only: a tracked nameplate is drawn on the map every frame, outside the vanilla distance cull and the Nameplate Cull patch, so villages stay unmarked. The player removes a mark with the Track toggle on the settlement's encyclopedia page, and the engine also drops a mark when a siege at that settlement ends, for settlements of the player's faction that were not tracked by hand and while the auto-track setting is below 2 (`SettlementNameplateVM.OnSiegeEventEndedOnSettlement`, `:624-649`). Performance: each mark keeps that nameplate drawn every frame; a kingdom reveal (Hard Questions) marks up to about 27 (the largest kingdom at start), most 4 to 16. `Settlement.HasVisited` is no longer written, because nothing in v1.5.5 reads it. Each newly known hero prints vanilla's "You've learned about" line, and the engine itself places his last-seen settlement. The reveal does not mark anyone as met. This is `MapRevealAdapter`, separate from `IPlayerIdentityAdapter.MarkClanAndKingdomKnown`, which the Player Switcher owns.
- **Force, at the end of the conversation:**
  - Relation: the lord -20 (clan) or -40 (kingdom); his clan leader -12 or -24; his spouse, parents, siblings and children -10 or -20; on the kingdom topic only, his king -16. See "Relations" below for how these combine.
  - Traits: Mercy -160 or -300, Honor -100 or -200, scaled by side. The player gains 80 or 150 Roguery experience.
  - Companions with Mercy above 0 lose relation: the cap (-2) at Mercy level 2, half of it (-1) at level 1.
  - Outcome: on the kingdom topic, an already wounded lord dies with a 5% roll. The Honor cost of the death (200 extra experience) is charged only when the lord really dies; the engine may refuse the kill, and then the lands are revealed as for any survivor. A death reveals nothing, and a dungeon death also removes the dead lord's character from the prison location. Otherwise a lord who is not wounded has a 15% roll to be wounded (his hit points are set to 1), on either topic.
- **Party screen:** a conversation opened from the party screen leaves the screen's pre-conversation snapshot alive, and closing the screen with Escape refills the prison and item rosters from it. After a force or a strip, `CommitOpenPartyScreen` goes through the shared `PartyScreenCommit.CommitIfMainParty`: it finds the open `PartyState` on the game-state stack and, only when the screen's right owner is the main party, calls `PartyScreenLogic.DoneLogic(true)` (vanilla commits unguarded when a companion is removed under the screen). The one skipped case is the ransom broker's screen with prisoners on the broker's side: it binds the live item roster but cloned troop rosters, and its Done handler sells the broker's side again. That skip no longer applies to Hard Questions, which offers no lines on the ransom screen (#1001); it still serves the gifts and Provender (Sworn Anew refuses a defection on the ransom screen itself).

## The strip

- Every piece in his own battle, civilian and stealth sets goes to the player's inventory through `IArmouryPlayerAdapter.AddPiece`, which keeps the item modifier. Pieces are grouped by (item, modifier): the count is the most any single set holds, so one sword worn in two sets is one sword and two identical stacks in one set are two. If the inventory refuses a group, none of its slots is cleared.
- **Named items stay with him** (decision D1): any item with `IArmourGateService.GetClass(id) == ArmourClass.Named`, the named hero weapons and the named hero armour. Armour Acquisition never loots named pieces, so this is the consistent rule.
- An empty body slot in the battle and civilian sets gets `torn_bandit_clothes`; a body slot still holding a kept named piece is left alone.
- **Shared-equipment guard:** `LordEquipmentAdapter` compares each set by reference with the engine's shared fallback and never reads, writes or re-kits a shared one.
- **On release** (`HeroPrisonerReleased`), a living stripped lord gets a fresh battle and civilian kit from `EquipmentSelectionModel.GetEquipmentForHeroComeOfAge`, which `TaomEquipmentSelectionModel` makes race- and culture-matched. The kit is copied slot by slot into his own sets, only into slots that are empty or hold the rag (`KitSlotRule`), and only for kit slots that have a piece, so a named weapon he kept stays. The stealth set is looted but not refilled: no NPC lord's stealth set is rendered in v1.5.5, and TAOM's kit donor refuses Stealth. A client that defers to the host does nothing.
- **Cost:** Honor -1000 (scaled by side), clan renown -200 (never below the current tier's floor), relation -100 with the lord, -20 with the other lords of his clan, -15 with the lords of his kingdom, -10 with every other lord whose Honor is above 0. Companions with Honor above 0 lose relation: -10 at level 2, -5 at level 1. The first three companions who lose any show a reproach line.

## Alignment scaling (decision D3) and relations

Every relation change goes through `IExecutionRelationService.GetRelationModifier` with the player as executor and the captive as victim. Each party is one `SideIds` (the map faction's id with that faction's culture, `FactionSides`): the executor is `IArmouryPlayerAdapter.Side` and the victim and every evaluator is `LordRef.Side`, so a lord of a Neutral realm is Neutral whatever the lord's own culture (#957). `LordRef.KingdomId` stays for the realm-wide relation set. When the sides are enemies only lords on the victim's side take the hit; when they share a side everyone takes 1.5 times. Trait experience losses use a JSON multiplier: 0.5 for enemy sides, 1.5 for the same side, 1.0 when either side is Neutral.

The engine ignores `affectRelatives` (v1.5.5). Whose relation with the player a change moves depends on the active DiplomacyModel's `GetHeroesForEffectiveRelation`, which `HardQuestionsPenaltyApplier.ApplyRelations` reads for each lord (`LordRef.RelationId`, filled by `CaptiveLordAdapter.Circle` from the engine's own model). It keeps the strongest delta per relation owner and charges each owner once, whatever order the hits come in. The clan leader (`CaptiveCircle.ClanLeader`, force) and the clan's other lords (`CaptiveCircle.ClanLords`, strip) are in the hit list in both modes, and the mapping decides what they do:

- **Personal Relations on (the default in single player; co-op uses the vanilla mapping):** `TaomDiplomacyModel` keeps the player's pair on the lord himself, so each lord is his own owner and takes his own delta. A force costs the clan leader -12 (clan topic) or -24 (kingdom topic) on top of the victim's own hit, and a strip costs each other lord of his clan -20.
- **Personal Relations off (vanilla mapping):** every clan member's relation lives on the clan leader, so the clan leader and the clan lords share the victim's owner and lose the merge to his stronger hit; there is one delta per clan leader and the strongest wins. The two modes are pinned by tests with distinct and with shared `RelationId`s.

A Neutral victim is nobody's ally in the alignment rule, so it would charge even his own relation owner nothing. For that owner and for the lords of his own clan (matched by clan id, which matters under Personal Relations where they are not his owner), the delta is at least the base hit. Every other lord, whatever his side, of his kingdom or not, is unmoved, as in executions. After a force or a strip, `ApplyRelations` shows one quick message the way vanilla's execution does: "Your deed has hurt your relations with {COUNT} lords" (singular for one). The count is the relation owners the deed changed (a lord, or his clan leader when Personal Relations is off), after the side scaling, the Neutral floor and the strongest-per-owner merge; an owner already at -100 still counts, as in vanilla's execution line. Companion losses are not counted, a bribe shows nothing, and a dedicated server shows nothing.

## Save record

`HardQuestionsState` is saved by `HardQuestionsCampaignBehavior.SyncData` under the key `_taom_hardQuestions` as one `Dictionary<string, string>`: `v` and `used|<heroId>`, whose value is a bit field per hero (1 clan topic, 2 kingdom topic, 4 stripped; anything outside 1 to 7 is dropped as damaged). Each topic and the strip work once per captivity. `HeroPrisonerReleased` clears the lord's entry (and re-kits a stripped living lord), and `HeroKilledEvent` clears it when a lord dies in captivity, because `EndCaptivityAction` returns at `Death` before raising the release event. A recaptured lord can be asked again. A save without the key loads as an empty state, and damaged entries are dropped with a warning.

## Configuration

### MCM

`World/Hard Questions`, `EnableHardQuestions` (default on, no restart needed, `TaomSettings.cs`).

### Config File: `Main/_Module/ModuleData/hard_questions/hard_questions.json`

Every number above is a field. Chances are in [0, 1], penalties are at most 0 (and at least -1,000,000), rewards are in [0, 1,000,000], multipliers are in [0, 1000]. NaN and infinity are rejected before the range check. A value that fails reverts to its default with a warning, followed by one summary warning. A missing or unparseable file uses the defaults. The file is cached for the process, so an edit needs a full game restart, not a new campaign or a save load.

## Key Files

| File | Purpose |
|------|---------|
| [InterrogationService.cs](../../Main/Features/HardQuestions/InterrogationService.cs) | Eligibility, price, bribe, the queued deed, force |
| [StripService.cs](../../Main/Features/HardQuestions/StripService.cs) | Strip, rags, release kit |
| [HardQuestionsPenaltyApplier.cs](../../Main/Features/HardQuestions/HardQuestionsPenaltyApplier.cs) | Relation, trait and companion costs |
| [HardQuestionsState.cs](../../Main/Features/HardQuestions/HardQuestionsState.cs) | Saved record |
| [HardQuestionsConfig.cs](../../Main/Features/HardQuestions/HardQuestionsConfig.cs) | Config DTO and defaults |
| [HardQuestionsConfigProvider.cs](../../Main/Features/HardQuestions/HardQuestionsConfigProvider.cs) | JSON load and validation |
| [HardQuestionsField.cs](../../Main/Features/HardQuestions/HardQuestionsField.cs), [HardQuestionsFieldKind.cs](../../Main/Features/HardQuestions/HardQuestionsFieldKind.cs) | One validated value and its rule |
| [IHardQuestionsConfigProvider.cs](../../Main/Features/HardQuestions/IHardQuestionsConfigProvider.cs), [IHardQuestionsSettingsProvider.cs](../../Main/Features/HardQuestions/IHardQuestionsSettingsProvider.cs) | Provider interfaces |
| [HardQuestionsSettingsProvider.cs](../../Main/Features/HardQuestions/HardQuestionsSettingsProvider.cs) | MCM toggle over a default of on |
| [HardQuestionsIoC.cs](../../Main/Features/HardQuestions/HardQuestionsIoC.cs) | Singleton registrations |
| [HardQuestionsModule.cs](../../Main/Features/HardQuestions/HardQuestionsModule.cs) | Feature module, behaviors built per campaign |
| [Domain/](../../Main/Features/HardQuestions/Domain/) | `LordRef`, `CaptiveSnapshot`, `CaptiveCircle`, `CompanionRef`, `LordEquipmentPiece`, `KitSlotRule` and the enums |
| [HardQuestionsDialogBehavior.cs](../../Main/Features/HardQuestions/Hooks/HardQuestionsDialogBehavior.cs) | Dialog conditions and consequences |
| [HardQuestionsDialogLines.cs](../../Main/Features/HardQuestions/Hooks/HardQuestionsDialogLines.cs) | The conversation tree |
| [HardQuestionsCampaignBehavior.cs](../../Main/Features/HardQuestions/Hooks/HardQuestionsCampaignBehavior.cs) | SyncData, release, death |
| [CaptiveLordAdapter.cs](../../Main/Adapters/CaptiveLordAdapter.cs) | Captive snapshot (cheap), circle (per deed), conversation context, `IsRansomScreenOpen` |
| [LordEquipmentAdapter.cs](../../Main/Adapters/LordEquipmentAdapter.cs) | Equipment sets with the shared-set guard, slot-wise re-kit |
| [MapRevealAdapter.cs](../../Main/Adapters/MapRevealAdapter.cs) | Tracking marks on settlements, known heroes. Shared with Lord Courtesies and, through the by-id pair `FortificationsAround` and `Track`, with [Notable Services](notable-services.md) and [Inn Rounds](inn-rounds.md) (both through `HoldsRevealService`, which also reads `MapDiagonal` and `ShowHoldsMarkedNotice`) |
| [HardQuestionsActionAdapter.cs](../../Main/Adapters/HardQuestionsActionAdapter.cs) | Relation, relation summary line, gold, wound, death, renown, reproach, party-screen commit |
| [PlayerTraitAdapter.cs](../../Main/Adapters/PlayerTraitAdapter.cs) | Trait experience through `TraitLevelingHelper.OnIncidentResolved` |
| [hard_questions.json](../../Main/_Module/ModuleData/hard_questions/hard_questions.json) | Numbers |

## Dependencies

- The module owns save data (`OwnsSaveData`).
- Armour Acquisition: `IArmourGateService` (the Named class) and `IArmouryPlayerAdapter` (inventory, gold, the `Side` pair). D1 relies on `ArmourGateService` filling its class map at every game init, also with that feature's toggle off.
- Execution: `IAlignmentService` and `IExecutionRelationService`.
- Enlistment: `IHeroSkillXpAdapter` (Charm and Roguery experience).
- InitialChildGeneration: `IRandomSource`.
- CoopInterop: `ICoopSessionProvider` and `IDedicatedServerProvider`.

## Tests

`TAOM.Tests/Features/HardQuestions/`: service, strip, penalty (including the real alignment and relation services with a Neutral victim), state, kit-slot rule, config provider (one test per validation rule over every field), behavior session-reset and wiring tests.

## What was cut

No scream sound (it needs custom audio); the force path closes with a text line instead.

## Known limitation

If Armour Acquisition's gate fails at game init (it logs when it does), named pieces are not recognised and a strip loots them. Markets and loot then behave ungated too.

## In-game checklist (owed)

1. Ask both topics by bribe and by force on a prisoner in the player's party and on one in a player dungeon. The revealed towns and castles show tracking marks on the map; open one settlement's encyclopedia page and switch Track off to remove its mark.
2. Strip a lord who carries a named weapon and confirm it stays with him.
3. Save and load: the used topics stay used.
4. Release a stripped lord and confirm a fresh kit; release one who kept a named weapon and confirm he still has it.
5. Force a death from the party screen, then press Escape: the dead lord is not back in the prison roster. Before Escape, check that his row on the screen is gone or does nothing when clicked.
6. Strip from the party screen, move a troop, then press Escape: the loot is still in the inventory.
7. Set `woundChance` and `deathChance` to 1 in `hard_questions.json`, restart the game fully, force the clan topic on a healthy lord (he is wounded), then the kingdom topic on the same lord (he dies, nothing is revealed, the extra Honor cost applies).
8. Strip a dwarf lord and an orc or goblin lord and look at the rag in the conversation scene, the party-screen tableau and the encyclopedia.
9. Victim on the enemy side, Personal Relations on: a clan-topic force lowers his clan leader by 12 and each kinsman by 10; a strip lowers each other lord of his clan by 20. Same side: 1.5 times. Personal Relations off: his clan leader drops once, by the strongest hit. Neutral victim (an Umbar or Khand lord), Personal Relations on: a strip lowers each other lord of his clan by 20 and no lord of another clan.
10. After a force and after a strip, the summary line appears once, with the right lord count (singular for one lord).
11. A dungeon death: before you leave, note whether his standing figure still offers a conversation; then leave and re-enter the dungeon: he is gone.
12. In German, the 19 lines show the German text with `{PRICE}`, `{COMPANION}` and `{COUNT}` filled.
13. Ransom screen: no lines (#1001). The backstreet ransom screen hides the lines: open it and talk to a captive lord, and no Hard Questions line shows. The tavern broker's screen does not allow Talk, because a mission is running. Open the normal party screen and talk to a captive lord: the lines show.
14. Neutral realm (owed, #957): as a player whose clan serves a Neutral realm (Khand, Umbar, Shaghana or Abanissa) with a Free-culture hero, force a clan topic on a Free captive and on an Evil captive: both read the Neutral multiplier (Honor and Mercy at the base amount), not the same-side 1.5 times.

## Translations

The 19 `taom_hq_*` keys are registered and translated in all 12 languages (2026-11-12, #763).

## Changelog

- 2026-10-26 (#1001): every line and the strip are unavailable while the ransom screen is open; the screen's cloned rosters could sell, ransom back to life and pay for a lord killed from it.
- 2026-10-20 (#957, part 2): the executor, the victim and the evaluators are `SideIds` (the realm's id with the realm's culture); `LordRef.CultureId` is replaced by `Side` and the trait multiplier reads the same pairs.
- 2026-10-20: restored the clan-leader force hit and the clan-lords strip hit after Personal Relations moved the relation owner onto each lord, extended the Neutral floor to his clan, and made the summary count lords (#948; `docs/reviews/rca-hard-questions-2026-10-19.md`, Round 3).
- 2026-10-20: the reveal marks towns and castles with the map tracker instead of the unused visited flag.
- 2026-10-19: one summary line of relation damage after a force or a strip.
- 2026-10-19: deep-review fixes (relation owners, Neutral floor, party-screen commit, release kit, death record, companion scaling).
- 2026-10-18: feature added.

## GitHub Issue

- **Issue:** #896, Hard Questions
- **Status:** Closed with `triage-needs-ingame`; the regression work is in #948

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
