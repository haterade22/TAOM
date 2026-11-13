# Good Deeds

## Overview

Three acts that say something about the player now earn relation: letting a captured lord go, handing a hero prisoner to a settlement, and winning a town's tournament. How much depends on who the player is. Freeing a lord of the other cause earns little, delivering a Mordor captive to Minas Tirith earns much more, and cheering a champion of the other cause earns nothing. Every amount and every multiplier is an MCM setting under "Good Deeds".

## Why This Exists

- **Vanilla behavior:** a release in conversation pays +4 with the lord and nothing else, and the party-screen release pays nothing. A donated hero prisoner pays only when the captive's relation with the owner's clan leader is 0 or below: the square root of the captive's ransom value times 0.5 for a king, 0.25 for a clan leader and 0.1 for another lord, capped at 40, 30 and 20, then times 1 to 1.5 as that relation falls from 0 to -100 (at most 60, 45 and 30). Vanilla shows its own quick notification for that relation change. A tournament win pays renown and influence, no relation.
- **TAOM requirement:** relation is the currency of the political game (marriages, fiefs, kingdom votes, gifts), and these three acts are the ones a player expects to be thanked for. The reaction should follow alignment, as every other TAOM relation rule does.
- **Without this feature:** the same act earns the same, or nothing, whoever the player is and whoever is on the receiving end.

## Architecture

### Design Challenge

- The release has several routes in vanilla and one shared tail, `EndCaptivityAction.ApplyByReleasedByChoice`, which raises `CampaignEvents.HeroPrisonerReleased(prisoner, party, capturerFaction, detail, showNotification)`. Three routes are the player's, and they name the releasing party differently, so no Harmony patch is needed but the party has to be read with care:
  1. **The prisoner conversation:** `party` is the main party.
  2. **The party screen** (move the prisoner to the release side, then Done, or Talk and confirm "Apply Changes"): the transfer has already taken the prisoner out of the party, so the event arrives with `party == null` and `detail == ReleasedByChoice`. The `PartyState` is still the active game state while the event is raised, so the adapter reads `(GameStateManager.Current?.ActiveState as PartyState)?.PartyScreenLogic?.RightOwnerParty` and compares it with the main party. It never calls `PartyScreenHelper.GetActivePartyState()`, which `Debug.FailedAsserts` when no party screen is open.
  3. **The "let go" conversation in the player's own dungeon:** `party` is the settlement's `PartyBase`, accepted when `Settlement.OwnerClan` is the player's clan (never `party.Owner`).
- A player-clan **mobile** party other than the main party is never accepted: `MobileParty.RemoveParty` releases a removed party's hero prisoners as `ReleasedByChoice` with that party. An AI party's removal also raises the event with its own party, so the read order is detail, then a null party, then a settlement, and an AI event reads nothing beyond what it read before.
- A donation's relation is a number the game model returns, and vanilla's `CharacterRelationCampaignBehavior.OnPrisonerDonatedToSettlement` applies it, for the main party only, to `donatedSettlement.OwnerClan.Leader`. That path never goes through `IRelationGrantService`, so it has no landed count and no de-duplication (see Known Limits).
- The engine lands a relation change on the relation owner, not on the hero named: the clan leader unless Personal Relations is engaged ([relations.md](relations.md)). Three grants to one lord, the lord's spouse and the lord's clan leader can therefore be three changes to one person.
- The `TournamentFinished` listener order matters to TournamentRewards ([tournament-rewards.md](tournament-rewards.md)), so Good Deeds does not touch that behavior. It registers its own listener. An `MbEvent` puts each new listener at the head of its list and invokes from the head with no try, and `GoodDeedsModule` is listed after `TournamentRewardsModule`, so the Good Deeds listener runs first and catches its own faults. It writes only relation, which no later listener reads. TournamentRewards still runs before vanilla's handler, as before.

### Solution Approach

One service (`GoodDeedsService`), three thin entry points, no Harmony patch, nothing saved. `GoodDeedsModule` (registered in `FeatureModules.All`, no edit to `IoC.cs` or `SubModule.cs`) registers the services, declares the campaign behavior and declares the game model through `GameModelDecl`. TournamentRewards is unchanged and does not reference Good Deeds (a test pins it).

| Entry point | What it does |
|---|---|
| `GoodDeedsBehavior` release listener (`Hooks/`) | Listens to `HeroPrisonerReleased`. Only when the detail is `ReleasedByChoice` does it ask `IGoodDeedsHeroAdapter.ReadReleaseSource(party)` for three raw facts (the main party; no party with the player's party screen open; a settlement the player's clan owns), then passes them with the prisoner's id to `GoodDeedsService.OnPrisonerReleased`. A fault is logged and never reaches the engine's event invoke |
| `GoodDeedsBehavior` tournament listener | Listens to `TournamentFinished` and passes whether the winner is the player's character and the town's settlement id to `GoodDeedsService.OnTournamentFinished`. The body is inside a try that logs |
| `TaomPrisonerDonationModel` (`Models/`) | `DefaultPrisonerDonationModel` with only `CalculateRelationGainAfterHeroPrisonerDonate` overridden: base first, then `GoodDeedsService.DonationRelation(base, heroId, settlementId)`. The influence methods stay vanilla's |

**The rules** (all in `GoodDeedsService`, pure over raw facts the adapter reads):

| Deed | Who is paid | Amounts (defaults) | Multiplier by side |
|---|---|---|---|
| Release | the lord, the lord's living spouse, the lord's clan leader | +5, +2, +2 | player against the lord: same side 1.0, neutral 0.75, opposite 0.5 |
| Donation | the settlement owner's clan leader (vanilla applies it) | vanilla's gain plus 5, both scaled | captive against the settlement owner: opposite 2.0, neutral 1.0, same side 0 |
| Tournament | the owner's clan leader (skipped when the player's clan owns the town) and every living notable in the town | +1 each | player against that person's own side: same 1.0, neutral 1.0, opposite 0 |

- **Sides** come from `IAlignmentService.ResolveSide`, handed one `SideIds` per party (the shared pair type, built by `FactionSides.Of` in `GoodDeedsHeroAdapter`, which replaced Good Deeds' own `DeedSide`): the player's `Clan.PlayerClan.MapFaction` and its culture, the lord's map faction and culture, the captive's, the settlement owner clan's, each notable's. The adapter keeps `MapFaction` for a notable, so a notable reads the realm of the town. A minor clan that has a row in `alignment.json` sides by its row only while it serves no kingdom (a mercenary contract makes the employer its map faction). A missing faction or culture resolves to Neutral. "Neutral" means either side is Neutral; two Neutral parties are Neutral, not "same". That makes nine ordered pairings of three sides, each pinned by a row in `SideMultipliersTests`: the pairing is `SideMultipliers.For(first, second)` in `Features/Relations`, shared with [Long Memory](long-memory.md).
- **Rounding:** every scaled amount rounds half away from zero (2.5 is 3, 0.5 is 1), computed in `decimal` so 0.7 times 5 is 3.5 and not 3.4999999. The provider also rounds each multiplier to two decimals, because the slider hands back a float such as 0.6999999 for the 0.7 it shows. A grant that scales to 0 grants nothing. The donation value returned to the engine is the rounded whole number.
- **One grant per relation owner:** the adapter reads each hero's owner with `DiplomacyModel.GetHeroesForEffectiveRelation(player, hero, out _, out var owner)` and the service lists a `RelationAsk` for each hero and hands the list to `GrantStrongestPerOwner` (`Features/Relations`), which groups by `(owner ?? hero)` and keeps the strongest per owner (the first listed wins a tie; its own tests pin the rule). With Personal Relations off the lord and the lord's clan leader are one owner, so one grant of 5 is made, not 5 and 2. With it on they are two. This is the pattern in `HardQuestionsPenaltyApplier.ApplyRelations`.
- **Counted by what landed:** `IRelationGrantService.Grant` returns the change that landed (the Charm bonus can add to it, the -100..100 clamp can cut it); the tournament line is shown only when at least one grant landed, never from the ask.
- **Ignored:** a release that is not `ReleasedByChoice` (ransom, peace, escape, battle, compensation), a release from a party that is none of the three sources (an AI party, a player-clan mobile party other than the main party, no party with no player party screen open), a prisoner who is not a lord, a dead hero, the player as spouse or leader, and anything while the feature or that deed's switch is off. A captive stripped by Hard Questions still earns the release reward; there is no special rule. Hard Questions listens to the same event only to clear its own record, and changes no relation.
- **Authority:** a co-op peer that defers to the host (`ICoopSessionProvider.ShouldDeferToHost`) and a dedicated server earn nothing, and a donation there returns vanilla's own value.
- **No per-campaign state:** the service keeps no fields that change, so it needs no `ResetForNewSession`. The module still builds the behavior with `new` for every campaign, as every module does.

### Component Diagram

```
HeroPrisonerReleased ─> GoodDeedsBehavior ──────────────┐
TournamentFinished ─> GoodDeedsBehavior ────────────────┤
PrisonerDonationModel slot ─> TaomPrisonerDonationModel ┤
                                                        v
MCM "Good Deeds" ─> GoodDeedsSettingsProvider ──> GoodDeedsService ──> IGoodDeedsHeroAdapter (raw facts)
                                                   │    │    │         IGoodDeedsNoticeAdapter (one green line)
                                       IAlignmentService │  IRelationGrantService ─> IHeroRelationAdapter
                                        ICoopSessionProvider, IDedicatedServerProvider
```

## Configuration

### MCM: "Good Deeds" (`GroupOrder = 65`)

Defaults and ranges live in `GoodDeedsSettingsProvider`, and the provider clamps through `SettingClamp` (a non-finite float reverts to its default; a hand-edited json is clamped; a multiplier is rounded to two decimals).

| Setting | Default | Range |
|---|---|---|
| Enable Good Deeds (master) | on | |
| Reward Releasing A Lord, Reward Donating A Hero Prisoner, Reward Winning A Tournament | on each | |
| Release: Relation With The Lord, With The Lord's Spouse, With The Lord's Clan Leader | 5, 2, 2 | 0 to 20 |
| Donation: Bonus Relation | 5 | 0 to 20 |
| Tournament: Relation Per Person | 1 | 0 to 10 |
| Release: Same Side, Neutral, Opposite Side Multiplier | 1.00, 0.75, 0.50 | 0.00 to 5.00 |
| Donation: Same Side, Neutral, Opposite Side Multiplier | 0.00, 1.00, 2.00 | 0.00 to 5.00 |
| Tournament: Same Side, Neutral, Opposite Side Multiplier | 1.00, 1.00, 0.00 | 0.00 to 5.00 |

Every value is read when the deed happens, none per frame, so none needs a restart and the provider is not on the hot-path list. The settings are simulation-relevant for co-op (the fingerprint counts them: [coop-interop.md](coop-interop.md)). The labels and hints are gender-neutral, and each hint quotes its compiled default; the tournament amount hint also quotes its 0 to 10 range (`GoodDeedsSettingsProviderTests` compares them with the constants).

### Localization

One key, `{=taom_gooddeeds_tournament}The people of {TOWN} honor your victory.`, shown as one green message-log line. It uses "your" and no gendered verb or adjective, so every translation fits any player. The twelve language files carry the translated row (2026-11-12, #763); `GoodDeedsWordingTests` pins the English row to the C# literal and checks that each language row keeps `{TOWN}` and not the old spelling.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/GoodDeeds/GoodDeedsService.cs` | The three rules, the side scaling, the per-owner grouping, the grants |
| `Main/Features/GoodDeeds/GoodDeedsRules.cs` | Rounding half away from zero, the donation arithmetic |
| `Main/Features/GoodDeeds/GoodDeedsSettings.cs`, `GoodDeedsSettingsProvider.cs`, `IGoodDeedsSettingsProvider.cs` | The MCM group, clamped |
| `Main/Features/Relations/SideMultipliers.cs`, `RelationTarget.cs`, `RelationGrantServiceExtensions.cs` | The side pairing, the hero and ask records and the per-owner grant, shared with Long Memory |
| `Main/Adapters/RelationTargets.cs` | Builds a `RelationTarget` from an engine hero |
| `Main/Features/TaomSettings.cs` | The "Good Deeds" MCM group (labels, hints, ranges) |
| `Main/Features/GoodDeeds/Hooks/GoodDeedsBehavior.cs` | The release and tournament listeners |
| `Main/Features/GoodDeeds/Models/TaomPrisonerDonationModel.cs` | The donation model |
| `Main/Features/GoodDeeds/Models/DeedFacts.cs` | The raw-fact records the adapter returns, `ReleaseSource` included (the hero record is `RelationTarget`) |
| `Main/Features/GoodDeeds/GoodDeedsModule.cs` | Services, behavior and model declaration |
| `Main/Adapters/IGoodDeedsHeroAdapter.cs`, `GoodDeedsHeroAdapter.cs` | Hero, party, settlement and town facts from the engine |
| `Main/Adapters/IGoodDeedsNoticeAdapter.cs`, `GoodDeedsNoticeAdapter.cs` | The green line |
| `Main/_Module/ModuleData/taom_module_strings.xml` and the 12 `Main/_Module/ModuleData/Languages/<L>/std_taom_module_strings_*.xml` | The `taom_gooddeeds_tournament` row, English and seeded in each language (13 files) |

## Dependencies

- `IRelationGrantService` (Relations): the clamp, the landed count and the log line; `RelationsModule` owns the registration.
- `IAlignmentService` (Execution): the side of each party.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the host and server gates.
- `IModLogger` (Core): the donation log line and the adapter faults.

## Tests

- `GoodDeedsRulesTests`: the rounding (halves, negatives, decimal exactness, non-finite), and the donation arithmetic. The nine side pairings are pinned in `SideMultipliersTests`.
- `GoodDeedsServiceReleaseTests`: each amount, each multiplier, every side pairing including a missing faction, the per-owner grouping (lord and leader as one owner and as two, spouse and leader tie and strongest), a null, dead or player spouse, a lord with no clan, a dead leader, the releases that are ignored (not by choice, none of the three sources, non-lord, blank id), the source matrix (main party pays, party screen pays, a player-clan settlement pays, nothing else does), the switches, and the co-op and dedicated-server skips.
- `GoodDeedsServiceDonationTests`: vanilla plus the bonus, scaled, every pairing, half a point, vanilla returned untouched when off, blank or unreadable, and the co-op and server skips.
- `GoodDeedsServiceTournamentTests`: the owner and every notable, the player-owned town, no leader, dead people, the scaling by each one's own side, the per-owner grouping, the notice only when something landed, a loss, and the skips.
- `GoodDeedsSettingsProviderTests`: defaults, pass-through, every clamp, every non-finite fallback, the two-decimal rounding (0.6999999 gives 4 from 5), the group declared once at `GroupOrder = 65` with every property in it and no restart, the slider ranges, each hint quoting its compiled default (and the tournament hint its range), and gender-neutral labels and hints.
- `GoodDeedsWordingTests`: the English row equals the C# literal and each of the twelve language rows equals the current English.
- `GoodDeedsBehaviorTests` (needs the game): the release hand-off, the party read only for a release by choice, the catch, and the tournament listener's hand-off and catch.
- `GoodDeedsWiringTests`: the module listed once, the single-owner files untouched, the model declared for the campaign, a fresh behavior per campaign, the model body free of decisions, exactly one filler of the donation slot (a class deriving from the model, not a reader), no Harmony patch, the tournament listener inside a try, and TournamentRewards free of any Good Deeds reference.

## How to Change the Rewards

1. Amounts and multipliers: MCM, "Good Deeds". No code change.
2. A new default: change the constant in `GoodDeedsSettingsProvider` (the property initializers and the fallback share it), and the test that pins it. A persisted MCM value keeps the old default: rename the setting (and say why in a comment) to change what existing players see.
3. A new deed: add a method to `GoodDeedsService` that builds `RelationAsk`s and calls `GrantStrongestPerOwner`, a raw-fact read on the adapter, and one entry point. Never call the engine from the service.

## Known Limits

- **A donation bypasses `IRelationGrantService`.** Vanilla applies the returned number itself, so there is no landed count, no seam log line and no per-owner grouping for it. It is one grant to one person, so grouping has nothing to do; the vanilla path also adds the Charm bonus the same way the seam does. Vanilla shows its own quick notification for it.
- **A donation of a captive the owner already likes pays vanilla nothing, and Good Deeds adds the bonus.** Vanilla pays only at relation 0 or below; the bonus does not check the relation.
- **The opposite-side donation can ask for more than the engine keeps.** Vanilla's gain plus the bonus, times the opposite multiplier (2.0 by default), can reach 130 with the vanilla maximum of 60 before the engine clamps the relation at 100.
- **Good Deeds adds no message line for a release or a donation.** Vanilla's own +4 notice in the conversation is unchanged, vanilla shows its own quick notification for a donation, and the TAOM log carries a `[Relations]` line for each grant that landed.
- **The post-battle "You have fought well. You are free to go." release pays only vanilla's +4.** It is `EndCaptivityAction.ApplyByReleasedAfterBattle`, a different detail from a release by choice, so Good Deeds ignores it.
- **The rival-capture quest's bribe release pays on top of the quest.** The Good Deeds reward lands in addition to the quest's own.
- **A prisoner held by a player-clan mobile party other than the main party does not count.** `MobileParty.RemoveParty` also frees a removed party's hero prisoners by choice with that party, so a clan party is deliberately none of the three sources.
- **Good Deeds stays on while the player is enlisted.** It has no enlistment check, deliberately.
- **No cooldown on a release.** Each lord released earns the rewards again, as vanilla's own +4 does, so a lord who is captured and freed again earns again. The only limits are the amounts, the side multipliers and the switch.
- **A removed party also reports a release by choice.** `MobileParty.RemoveParty` frees the party's hero prisoners with `ApplyByReleasedByChoice`, so the same event fires for them. It earns nothing for an AI party (its own party is none of the three sources), and a removed main party is not a path the game takes in normal play.
- **A sold prisoner is not ransomed.** The issue's optional "sale means ransom" rule is not built (Mike, 2026-10-20): it needs a Harmony patch on `SellPrisonersAction` and changes AI dungeon populations, for a default-off balance toggle.

## In-game Checklist

Setup once: a campaign with cheat mode on. `campaign.add_hero_relation [Hero] | [OtherHero] | [Value]` ADDS the value to the relation between the two heroes (omit the other hero for your own relation), `campaign.declare_war` and `campaign.declare_peace` set the diplomacy, and `campaign.add_prisoner_to_party [PrisonerName] | [YourName]` puts a lord of a kingdom at war with you into your prison. Read the `[Relations]` and `[GoodDeeds]` lines in the TAOM log after each step.

1. **Release in conversation, same side.** Be Free (a Gondor, Rohan or Rivendell realm, or no realm with a Free culture), declare war on a Free kingdom, capture or add one of its lords, and let him go from the prisoner conversation. Expect +4 from vanilla and +5 from Good Deeds with the lord, +2 with the lord's living spouse (if any) and +2 with the lord's clan leader (if the lord is not the leader). Check each with the encyclopedia relation or the log.
2. **Release from the party screen.** Same lord: move the lord to the release side of the party screen and press Done. The same Good Deeds rewards as step 1 (a party-screen release now pays), with no vanilla +4.
3. **Release in your own dungeon.** Hold a lord in the dungeon of a town or castle your clan owns and use the "let go" conversation there. The same Good Deeds rewards as step 1.
4. **Opposite side.** As a Free player, release a lord of an Evil kingdom (Mordor, Isengard, Gundabad): +3 with the lord (5 times 0.5, rounded up), +1 with the spouse and the leader.
5. **Neutral.** Release a Khand, Umbar, Shaghana or Abanissa lord: +4 with the lord (5 times 0.75, rounded up), +2 with the others.
6. **One owner.** Turn Personal Relations off in MCM, release a lord who is not the leader of the lord's clan: one grant of 5 lands on the clan leader, and the log shows one `[Relations]` line for the clan, not two. Turn it on: one line each for the lord, the lord's living spouse (if any) and the leader, so pick a lord with no living spouse to see two.
7. **Ignored releases.** Let a captive go by ransom or peace, take the post-battle "free to go" choice (vanilla's +4 only), release a non-lord (a companion or a notable), watch an AI party free a prisoner: no `[GoodDeeds]` or `[Relations] ... good deed` line.
8. **Switches.** Turn "Reward Releasing A Lord" off, release a lord: nothing. Turn the master off: nothing from any deed.
9. **Donation, opposite side.** As a Free player capture an Evil lord (or add him), enter a Free kingdom's town (Minas Tirith) and donate him. The owner's clan leader's relation rises by about (vanilla's gain plus 5) times 2. The log has `[GoodDeeds] donation of <hero> to <town>: vanilla V, bonus 5, x2 = N.`
10. **Donation, same side.** Donate a Free lord to a Free town: no relation change (multiplier 0), and the log line ends `= 0.`
11. **Donation, liked captive.** Check the captive's relation with the owner's clan leader in the encyclopedia first. `campaign.add_hero_relation [Captive] | [Owner leader] | [N]` adds N to it; pick an N that brings it above 0, where vanilla pays nothing. Then donate: vanilla pays nothing, Good Deeds still adds 5 times the multiplier.
12. **Donation switch.** Turn "Reward Donating A Hero Prisoner" off: the relation is vanilla's own.
13. **Tournament in a friendly town.** Win a tournament in a Free town as a Free player (a town you do not own): +1 with the owner's clan leader and +1 with each notable, and one green line "The people of <town> honor your victory." Losing, or being eliminated in a round, earns nothing and shows no line.
14. **Your own town.** Win a tournament in a town your clan owns: the notables get +1, the owner (you) none, the line still shows.
15. **Opposite side town.** As a Free player win a tournament in a town held by an Evil realm (a captured town counts by its owner): no relation and no line, because the multiplier is 0.
16. **Personal Relations off, tournament.** A notable and the owner's clan leader are different owners, so each gets +1 either way.
17. **Co-op.** On a co-op client, none of the three deeds grants anything; the host's own deeds do.
18. **MCM live.** Change an amount or a multiplier mid-campaign: the next deed uses it, no restart. A new campaign in the same session starts with the same behavior wiring and no carried state.

## Changelog

- 2026-10-21: the side pairing (`SideMultipliers.For`), the hero record (`RelationTarget`, formerly `DeedHero`) and the strongest-per-owner grant (`GrantStrongestPerOwner`) moved to `Features/Relations`, shared with Long Memory; `SidePairing` and `GoodDeedsRules.Pair` are gone and the behavior is unchanged.
- 2026-10-20 (#957, part 2): `DeedSide` is replaced by the shared `SideIds`; the minor clan rows in `alignment.json` side the Axemen of Erebor and the Druedain.
- 2026-10-20: review fixes. A party-screen release and a release in the player's own dungeon now count, and the release source is three raw facts read by the adapter and decided by the service. The tournament step is Good Deeds' own listener, and TournamentRewards is back to its previous content. Multipliers round to two decimals, labels and hints are gender-neutral, the notice reads "honor", and the donation formula and the known limits are corrected.
- 2026-10-20: first version (#864). Release by choice, donation model and the tournament step, scaled by alignment, de-duplicated per relation owner, MCM group at `GroupOrder = 65`.

## GitHub Issue

- **Issue:** #864, [Relation rewards for releasing prisoners, donating them and winning tournaments](https://github.com/haterade22/TAOM/issues/864)
- **Status:** Shipped on `bannerlord-1.5.x` in `ad8772a3e` (v2.0.35). Closed with `triage-needs-ingame`: the in-game checks are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/long-memory.md](./long-memory.md)
- [docs/features/relations.md](./relations.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
