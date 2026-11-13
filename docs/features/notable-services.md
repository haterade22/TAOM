# Notable Services

## Overview

Three lines under the main conversation menu of a notable (an artisan, gang leader, merchant, rural notable or headman; a preacher does not offer them) of the settlement the player stands in. "I have men to spare for your people" ends the conversation and opens a party screen where the player gives troops and prisoners to the settlement: troops raise its militia, prisoners raise a village's hearth or a town's prosperity, and the notable thinks better of the player. "Introduce me to the other notables here" pays the speaker to mark the settlement's other unmet notables as met. "Point me to the holds near here" pays the speaker to have the nearest towns and castles marked on the map.

No Harmony patch, no GameModel and no save data: one dialog behavior over a service graph. Issue #899.

## Why This Exists

- **Vanilla behavior:** a notable offers quests, recruits and trade. A player can give nothing to a settlement, meets each notable only by walking up to him, and learns the map by travelling it.
- **TAOM requirement:** a settlement that is shown goodwill and troops should answer in kind, and a notable who knows the neighbourhood should be able to tell a stranger where the strongholds stand. Prisoners are a Roguery trade; handing them over is how an outlaw pays a community.
- **Without this feature:** militia and hearth rise only by the engine's own drift, notables stay strangers until visited, and the map stays dark until the player has travelled it.

## Decisions (Mike, 2026-10-24)

1. **Introduce** calls `Hero.SetHasMet()` on each unmet notable of the settlement other than the speaker. `IsKnownToPlayer` is not written: vanilla's menus already set it (`HeroKnownInformationCampaignBehavior.cs:164-180`).
2. **Reveal targets** are towns and castles only, marked through the shared `MapRevealAdapter` (`VisualTrackerManager.RegisterObject` behind a `CheckTracked` guard).
3. **Nearby** means the `revealCount` (3) nearest untracked fortifications by straight-line distance from the speaker's settlement, within `revealMaxDiagonalFraction` (0.2) of `Campaign.MapDiagonal`.
4. **Prisoners on the screen** are filtered by the prisoner's side against the settlement's side. Mike approved these outcomes: a Free settlement refuses Evil prisoners, an Evil settlement accepts Free prisoners, and a Neutral side on either end accepts all. (Written directly in `RefusesPrisoner`: only a Free settlement refusing an Evil prisoner is refused.)
5. **Troops** have no alignment filter.
6. **Gain** is `min(maxGainPerGift, men x gainPerMan / max(1, 1 + relation / 10))`, granted only when it rounds to at least 1, through `IRelationGrantService`.
7. **No wait** after a gift.
8. **Co-op gate:** the Settlement Services single-player predicate, `CoopSessionPolicy.IsSinglePlayerAuthority` (no live session, no deferral to a host, no dedicated server, and an authority).
9. **Shared reveal:** the reveal's rules live in `HoldsRevealService` and its engine calls in `IMapRevealAdapter`, so the tavern keeper's line ([inn-rounds.md](inn-rounds.md), #901) uses the same ones. The caller decides who may ask and how the price is paid; the service decides the outcome.

## Architecture

### Design Challenge

- `PartyScreenHelper.OpenScreenAsLoot` lets heroes of other clans move (`PartyScreenHelper.TroopTransferableDelegate`, `:387-395`), so the screen is built from `PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty` with two dummy rosters on the left. `PartyScreenLogic.DoneLogic` hands the left rosters to the done delegate; Cancel resets the screen (`ResetUsing`), which puts every man back.
- The engine asks the transfer delegate when it builds a row and once per stack in Transfer All, never on a click or a drag, and it sees one character, not an amount. So any click or drag can pass the cap, and the delegate cannot hold it. The done condition is re-checked after every move and is what enforces it (Done is refused above `maxMenPerGift`), and each left panel's size limit is set to the cap, which bounds a Transfer All on that panel only (the engine has no combined limit, so both panels read "count/cap" and troops plus prisoners together can pass it until Done refuses). The delegate also refuses a move that builds a row or a Transfer All stack once the gift side already holds the cap, and the service counts at most the cap, troops first.
- The screen mode is `Shared`. `TroopsManage` and `QuestTroopManage` hide both prisoner panels (`PartyVM.cs:2059`) while the D hotkey still moves prisoners the player cannot see.
- A screen can commit more than once: Done, and the forced `DoneLogic(true)` that the party screen runs when a companion is removed (`GauntletPartyScreen.OnCompanionRemoved`), which skips the done condition. The map bar cannot commit it, because any non-Normal party screen locks the map-bar buttons. The service remembers what it has already paid and pays only the increase.
- The gift writes the live rosters, so an older party screen under it would keep a stale saved copy and its Escape would restore the given men while the credits stay. The gift line is therefore hidden while any party screen is on the state stack, and the after-conversation step checks again before opening.
- `Hero.IsNotable` is true for a preacher, so the service excludes `IsPreacher`.
- A messenger conversation starts a real encounter and moves the main party into the target's settlement, so the engine state looks like a visit. All three lines read `IMessengerConversationState.IsOpen` and hide.

### Solution Approach

`NotableServicesService` owns every rule over snapshots and ids. `INotableServicesAdapter` is the engine boundary for the speaker, the unmet notables, the purse, the gift screen, the settlement writes, the player hero id and the notices. The Roguery experience goes through the shared `IHeroSkillXpAdapter.AddSkillXpLanded`, which returns the experience that landed. `IMapRevealAdapter` gains `FortificationsAround(settlementId)` (every other town and castle with its distance and tracked flag), `Track(ids)`, `MapDiagonal` and `ShowHoldsMarkedNotice(count)`; the clan and kingdom reveals share its one marking site. `HoldsRevealService` owns the reveal's order: war gives `AtWar`, a relation under `revealMinRelation` gives `Cold`, no candidate gives `NothingNew`, then the caller's payment, the marks and the notice. `NotableServicesService.Reveal` finds the speaker, builds a `RevealAsk` (settlement, war flag, relation) and pays the notable through `PayNotable`. `NotableServicesDialogBehavior` is thin (ADR-002) and built with `new` for every campaign because it keeps the last answers its reply lines read.

### Component Diagram

```
notable_services.json -> NotableServicesConfigProvider (validated, cached)
                                  |
NotableServicesDialogBehavior -> NotableServicesService (Gain, rules, gate)
(lines at priority 111)               |
                                      +--> INotableServicesAdapter (snapshot, purse, screen, writes, notices)
                                      +--> HoldsRevealService --> IMapRevealAdapter (FortificationsAround, Track, MapDiagonal, notice)
                                      +--> IRelationGrantService, IAlignmentService
                                      +--> IMessengerConversationState, ICoopSessionProvider, IDedicatedServerProvider
```

## What the player sees

The three options sit on `hero_main_options` at priority 111 (vanilla is 100). They show for a notable (`Hero.IsNotable`: artisan, gang leader, merchant, rural notable or headman) who is not a preacher and stands in the settlement the main hero is in. War hides only the Give men line: Introduce still works at war, and the pointing-out answers with its war refusal. All three are hidden during a messenger conversation, outside single-player, and with the MCM toggle off.

- **Give men** ("I have men to spare for your people"): hidden at war with the settlement's realm, while any party screen is open, and when the player holds no regular troop or prisoner. The notable answers, the conversation ends, and the "Give Men" party screen opens with both prisoner panels shown. Troops and prisoners move from the player to the settlement. Heroes never move, upgrades are off, nothing is donated to a party, and talking from the screen is refused. At a castle prisoners cannot move. A prisoner the settlement refuses (decision 4) cannot move. Cancel changes nothing. On Done: each troop adds `militiaPerTroop` to the settlement's militia; each prisoner adds `hearthPerPrisoner` to a village's hearth or `prosperityPerPrisoner` to a town's prosperity; the relation gain is granted with the notable; Roguery experience of `rogueryXpPerPrisoner` per prisoner is asked for, and the notice shows the experience that landed after the engine's multipliers (none when none did); other notices show the men taken and the relation earned.
- **Introduce** ("Introduce me to the other notables here (N denars)"): shown only when at least one other notable of the settlement is unmet. The price is the number of unmet notables times `introducePrice`. The line is greyed with "You don't have enough gold." when the player is short. The consequence pays the speaker (a positive `GiveGoldAction` amount from the player to him), then marks each unmet notable as met. If the payment cannot be made nobody is marked.
- **Point out the holds** ("Point me to the holds near here (N denars)"): always shown while the lines are, greyed when the player is short. The work runs in the consequence, never in a condition, and the reply reads the stored outcome. At war: refused, with no charge. Relation below `revealMinRelation`: refused, no charge. No new candidates (all within range already marked, or none within range): "nothing more to tell", no charge. Otherwise `revealPrice` is paid to the speaker once and the nearest `revealCount` untracked towns and castles within the bound get the engine's visual tracker mark. The settlement the player stands in is never a candidate.

## Configuration

### Config File: `Main/_Module/ModuleData/notable_services/notable_services.json`

Every value is a finite float. A NaN, infinite or out-of-range value reverts to its default with a warning, and one summary warning follows; a missing or unparseable file uses the defaults. Cached for the process, so an edit needs a restart. A test pins the shipped file to the compiled defaults.

| Key | Default | Range | What it does |
|---|---|---|---|
| `introducePrice` | 50 | 0..100000 | Denars per unmet notable introduced |
| `revealPrice` | 100 | 0..100000 | Denars for one pointing-out |
| `revealCount` | 3 | 1..12, rounded | Towns and castles marked per pointing-out |
| `revealMaxDiagonalFraction` | 0.2 | (0, 1] | Farthest hold named, as a share of the map diagonal |
| `revealMinRelation` | 0 | -100..100 | Relation needed before he names the holds |
| `gainPerMan` | 0.5 | 0..10 | Relation per man before the divisor |
| `maxGainPerGift` | 20 | 0..100 | Most relation one gift earns |
| `militiaPerTroop` | 1 | 0..10 | Militia per troop given |
| `hearthPerPrisoner` | 1 | 0..10 | Hearth per prisoner given to a village |
| `prosperityPerPrisoner` | 1 | 0..100 | Prosperity per prisoner given to a town |
| `rogueryXpPerPrisoner` | 10 | 0..1000 | Roguery experience per prisoner given |
| `maxMenPerGift` | 100 | 1..1000 | Most men one gift counts and the screen lets move |

The gain rounds half away from zero, and a gain under 1 is not sent. At relation 50 the divisor is 6; at relation below -9 it is 1, because it never drops under 1.

### MCM

`World / Notable Services` (`GroupOrder = 77`): **Enable Notable Services**, default on. The next free order below the Map Tools sentinel (100) was used.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NotableServices/NotableServicesService.cs` | Every rule: gate, gain, transfer filter, introduce, reveal |
| `Main/Features/NotableServices/HoldsRevealService.cs` | The reveal's rules, shared with the tavern keeper's line |
| `Main/Features/NotableServices/NotableServicesModule.cs` | Feature module, behavior built with `new` per campaign |
| `Main/Features/NotableServices/NotableServicesIoC.cs` | DryIoc registration (never the map reveal adapter) |
| `Main/Features/NotableServices/NotableServicesConfigProvider.cs` | Validated JSON config |
| `Main/Features/NotableServices/NotableServicesSettingsProvider.cs` | MCM switch over a default of on |
| `Main/Features/NotableServices/Domain/` | `NotableSnapshot`, `GiftTally`, `GiftTransfer`, `IntroduceQuote`, `RevealAsk`, `RevealOutcome`, `SettlementKind` |
| `Main/Features/NotableServices/Hooks/` | Dialog behavior, dialog lines, number-carrying texts |
| `Main/Adapters/INotableServicesAdapter.cs`, `NotableServicesAdapter.cs` | Engine boundary |
| `Main/Adapters/IMapRevealAdapter.cs`, `MapRevealAdapter.cs`, `RevealCandidate.cs` | Shared reveal, extended |
| `Main/_Module/ModuleData/notable_services/notable_services.json` | Configuration data |

## Dependencies

- `IMapRevealAdapter` (Adapters): registered by Hard Questions and only resolved here (by `HoldsRevealService`); a test pins one registration in the whole mod.
- `IHeroSkillXpAdapter` (Enlistment registers it): the Roguery experience and the amount that landed.
- `IRelationGrantService` (Relations): the relation grant; the notice shows the change that landed. The relation owner of a clanless notable is the notable himself, so the grant names the speaker.
- `IAlignmentService` (Execution): resolves the two sides for the prisoner filter.
- `IMessengerConversationState` (Messengers), `ICoopSessionProvider`, `IDedicatedServerProvider` (Co-op Interop): the gates.
- `ScreenFaultLog` (Core): every engine-called member of the behavior runs behind it.

## Tests

- `TAOM.Tests/Features/NotableServices/NotableServicesServiceTests.cs`: the gain formula, where the men go, the transfer filter, who offers and when, introduce, reveal and Roguery experience.
- `NotableServicesConfigProviderTests.cs`: every field against NaN, both infinities, below and above range, the bounds, a missing and an unparseable file, and the shipped JSON.
- `NotableServicesWiringTests.cs`: the module line, nothing in `IoC.cs` or `SubModule.cs`, one map reveal registration, the container resolving the service, the entry-point size, source pins that no condition writes, the screen's construction and the settings group.
- `HoldsRevealServiceTests.cs`: the shared reveal's outcomes (war, cold, nothing new, an unpaid price, a free reveal, a fault), its order and its config reads.
- `NotableServicesBehaviorTests.cs` (`RequiresGame`): the behavior over the real service.
- A campaign behavior declared by a feature module has its co-op row in [coop-interop.md](coop-interop.md).

## How to verify in game

1. Troops to a village, a town and a castle: the militia rises in each.
2. Prisoners to a village (hearth rises), a town (prosperity rises) and a castle (they cannot be moved).
3. A Free village refuses orc prisoners. A Mordor village takes Free prisoners.
4. Cancel the screen: nothing changes.
5. The relation notice and the Roguery notice (the experience that landed, which can differ from the ask) show, and Done refuses a gift above the cap.
6. Introduce: the gold goes to the speaker, and the encyclopedia "Met Before" lists them.
7. Reveal at peace marks holds. At war or at negative relation it is refused with no charge. Each repeat marks the next nearest holds, and "nothing more to tell" (with no charge) comes only once every hold within reach is marked.
8. A messenger conversation shows none of the lines.
9. Save and reload: the marks and the met flags persist.
10. Prisoners show on both sides, and the D key behaves.
11. Party screen, then Talk to a companion, then a notable: the gift line is hidden.
12. Done and Cancel from a town conversation return to the town cleanly.
13. Repeat the pointing-out until about 30 holds are marked, then compare map frame time with the marks on and cleared (the A/B in `nameplate-cull.md`).

## Performance

Each repeat of the pointing-out marks the next 3 holds. From one place the player can reach a median of about 76 towns and castles within the 0.2 bound (counted by a script over the live `settlements.xml`; not re-run for this doc). Every tracked nameplate is drawn on each map frame, outside the vanilla distance cull and the Nameplate Cull patch, and that cost has not been measured. The tavern keeper's line ([inn-rounds.md](inn-rounds.md)) sells the same marks through the same service, so a player who uses both reaches that cost sooner, and the unmeasured nameplate cost counts for both. The player removes a mark with the Track toggle on the settlement's encyclopedia page.

## Known limits

- **Cap on a click or drag.** The screen's transfer delegate is not asked on a click or a drag, and it sees one character, not an amount, so a move of a large stack can pass the cap. Each panel's size limit is the cap on its own, so troops and prisoners together can also pass it. The done condition, re-checked after every move, refuses the gift until it is back under it.
- **A forced commit can strand credit.** After a forced commit (a companion removed while the screen is open; this is not verified as reachable from a town conversation), men already credited can be taken back on the screen and stay credited. This is not guarded on purpose, because the case is speculative.

## Translation

The 12 language files hold translated rows (2026-11-12, #763).

## Changelog

- 2026-10-26: the Roguery notice shows the experience that landed, not the ask: `IHeroSkillXpAdapter.AddSkillXpLanded` returns the change in the hero developer's Roguery total (the engine scales the ask by the XP multiplier and the learning rate), the service injects that shared adapter, and the notice is skipped when none landed. `INotableServicesAdapter` lost its own Roguery call and gained `PlayerHeroId`. The single-player gate is now `CoopSessionPolicy.IsSinglePlayerAuthority`.
- 2026-10-26: the reveal's rules move into `HoldsRevealService` for Inn Rounds (#901); `MapDiagonal` and `ShowHoldsMarkedNotice` move from `INotableServicesAdapter` to `IMapRevealAdapter`. Behaviour and the NotableServices tests are unchanged (the fixture and one assertion read the moved members from the reveal adapter).
- 2026-10-26: wave 2 review fixes: the relation notice uses the game's own translated relation line, the experience notice rounds like every other amount, the pointing-out walks only towns and castles, and the docs name the per-panel cap and the reveal's nameplate cost.
- 2026-10-26: wave 1 review fixes: prisoner panels shown (mode Shared), gift hidden under any open party screen and when there is nothing to give, the relation notice shows what landed, the switch is player-local in the co-op fingerprint.
- 2026-10-24: first version (#899).

## GitHub Issue

- **Issue:** #899, Notable Services
- **Status:** Open
