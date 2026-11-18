# Hostile Peace

## Overview

Free and Dark Power realms stay at peace until the War of the Ring, but they do not act as friends. Two realms whose
`diplomacy.json` tier is `Hostile` (every Free realm against every Dark Power, plus the extra Hostile rows in
`diplomacy.json` and a pledged realm through War Pledge) get blocks A to C while they are at peace, and block D at all times:

| Block | What stops | Where |
|---|---|---|
| A, caravans | Their caravans do not trade in each other's towns | `Patch156_CaravanTrade_HostilePeace`, a postfix on `CaravansCampaignBehavior.CanTradeWith` |
| B, trade agreements | They never sign a trade agreement | `TaomTradeAgreementModel.CanMakeTradeAgreement` |
| C, the player's access | The player meets their towns and castles as at war: a town only in disguise, a castle not at all (the gate and "Request entry to the castle" both), and no trade, recruits or rest in their villages | `TaomSettlementAccessModel`, `ProvenderWorldAdapter.RefusesPlayer` for the Provender-master |
| D, the player's alliances | A kingdom the player rules does not ally across the Free/Evil line (any phase, peace or war) | `PlayerAllianceGate`, asked by `TaomAllianceModel`, `TaomKingdomDecisionPermissionModel` and `PlayerAllianceProposalBehavior` |

Block D's switch (`AllowPlayerAlliancesWithAnyone`) follows the campaign's Alignment rules preset first (Strict or Relaxed pin it; Standard follows MCM), through `IHostilePeaceSettingsProvider`; see [campaign-start-options.md](campaign-start-options.md).

No war is declared and no phase is read. Each block has one switch in MCM ("World/Hostile Peace") and in
`ModuleData/diplomacy/hostile_peace.json`; MCM wins when it is loaded. A, B and C are blocks that default on; D's switch is
"Allow Player Alliances With Anyone" (`allowPlayerAlliancesWithAnyone`), default off, which keeps the block on.

**Block D in short.** Before 15 Nov 2026 the player's kingdom could ally with anyone ("Full freedom", [diplomacy.md](diplomacy.md)).
Now each kingdom is sided by `IAlignmentService.ResolveSide` on its own id and culture: a listed realm by its id (the player
ruling Mirkwood is Free), a kingdom the player founded by its culture (Gondor culture is Free, Mordor culture Evil). A Neutral
side on either end never blocks, so a kingdom with no side may ally with anyone. D reads the alignment table, not the Hostile
tier, because a player-founded kingdom is in no `diplomacy.json` row. The alliance score of a blocked pair gets the Hostile
tier's -10000 instead of the player bonus, the kingdom decision is refused with "Your realms stand on opposite sides of the war
and cannot be allied.", and the "I propose an alliance" line is hidden. AI realms keep their own Hostile-tier rule either way.
**Alliances made before** stay: the player's way out is unchanged (declaring war on the ally ends the alliance, and the Full
War on day 44 does it for a Hostile pair), so nothing is soft-locked, while ending them on load would change an old save
one-sidedly.

## Why This Exists

- **Vanilla behavior:** two kingdoms at peace trade freely. A caravan refuses only a town at war (`CanTradeWith`), a trade
  agreement is refused only at war or for its cap and borders (`DefaultTradeAgreementModel`), and the player is held at the
  gate only at war, for crime or for a low clan tier (`DefaultSettlementAccessModel`).
- **TAOM requirement:** since v2.0.34 every campaign opens at peace (no wars before day 30, [war-of-the-ring.md](war-of-the-ring.md)).
  A player as Thranduil found the Woodland Realm trading with Dol Guldur's orcs; the owner: "i dont want them at war, i meant
  that they trade etc, makes no logical sense" (15 Nov 2026).
- **Without this feature:** Free and Shadow caravans fill each other's markets, the AI signs trade pacts across the line,
  and the player shops and rests in Dol Guldur before the war.

## Design decisions

- **Hostile tier, not the alignment table.** The pair test is `IDiplomacyService.GetRelationshipTier(a, b) == Hostile`, the
  same one the War of the Ring uses for its Full War. The older side rules (recruitment, marriage, tournaments, Kingdom
  Allegiance) read `alignment.json`, which also counts Harad, Dunland and the orc realms as Evil; Hostile is narrower on
  purpose: Neutral and non-Hostile pairs trade as before.
- **Peace only.** Each block applies only to a pair not at war; at war the base game already refuses all three.
- **Kept after the War of the Ring ends.** A victory (`WarPhase.WarEnded`) lifts the peace locks, so two Hostile realms may make
  peace again. They are still on opposite sides, so the blocks stay. No phase is read anywhere in this feature.
- **The base game's war answer, not a new one.** Block C returns exactly what `DefaultSettlementAccessModel` returns at war
  (town: limited access by disguise; castle: no access, by request; village: trade, recruit and rest refused with the base
  game's texts). The menus read only those values: the sneak-in option is `LimitedAccessSolution.Disguise` and the closed-gate
  text is `AccessLimitationReason.HostileFaction` (`EncounterGameMenuBehavior`). A sneak caught at peace raises the crime rating
  and ends in captivity, the base game's own path for a peaceful town where the player has a crime rating.
- **Crime stops short of war (owner's rule, 2026-11-15: "Keep failed sneaks from starting a war, crime only").** A failed sneak
  adds crime, and at the engine's threshold (60) a ruler player's kingdom would declare war (`ChangeCrimeRatingAction`). So when
  the player leads a realm and the faction is a Hostile realm at peace, `TaomCrimeModel` caps the new crime rating at 59
  (`GetEffectiveCrimeChange`, `LimitMax(threshold - 1)`). It is a cap and not a block on the war because the action applies -10
  relation with the faction leader before it declares, and a blocked war would leave the rating over the threshold and repeat that
  loss on every later crime change. Crime still fines and still feeds the disguise detection; the severe-crime line (65) is not
  reached for that realm. A player who does not lead a realm is never declared against by crime and is not capped. Block C's
  "Enemy Realms Close Their Gates" switch turns the cap off with it.
- **The player's own fiefs are left alone.** Block C never limits a settlement the player's clan owns. It does not exempt the
  settlement the player is in: on v1.5.7 the engine sets `CurrentSettlement` before the gate menu asks the model, so such an
  exemption would always be true. No town or castle menu asks `CanMainHeroEnterSettlement` from inside. A town's keep and the
  dungeon keep the base rules (a town keep is reached only after a sneak in, where the base model answers as at war). A
  castle's keep does not: "Request entry to the castle" asks `CanMainHeroEnterLordsHall` from outside, so a Hostile castle
  gets the war answer there.
- **Old trade agreements run out.** An agreement signed before the switch was on runs to its end date; a proposed one is
  cancelled by the decision's own check.

## Architecture

```
hostile_peace.json -> HostilePeaceConfigProvider -> HostilePeaceSettingsProvider (MCM over JSON)
                                                          |
IDiplomacyService.GetRelationshipTier ---------> HostilePeaceService (pure: ids in, bools out)
                                                          |
             Patch156 (CanTradeWith postfix)   TaomTradeAgreementModel   TaomSettlementAccessModel
```

`HostilePeaceModule` registers the providers and the service and initializes Patch156. `SubModule.RegisterDiplomacyAndConflict`
adds the two game models. Nothing is saved.

**Co-op.** The trade agreement model defers to the host on a peer (`ICoopSessionProvider.ShouldDeferToHost`), like
`TaomDiplomacyModel`. The access model answers only the local player's own menus and has no gate. The caravan postfix has
none, like CaravanTrade's postfix on the same method.

**Coexistence with CaravanTrade.** `Patch59_CaravanTrade` also postfixes `CanTradeWith`, but only turns a war-caused `false`
into `true`; Patch156 only turns a peacetime `true` into `false`. The order of the two does not matter.

## Files

| File | Purpose |
|---|---|
| `Main/Features/HostilePeace/HostilePeaceService.cs` | The pair rule and the four block questions |
| `Main/Features/HostilePeace/HostilePeaceSettingsProvider.cs` | The four switches, MCM over JSON |
| `Main/Features/HostilePeace/HostilePeaceConfigProvider.cs`, `HostilePeaceConfig.cs` | `hostile_peace.json` |
| `Main/Features/HostilePeace/Hooks/Patch156_CaravanTrade_HostilePeace.cs` | Block A |
| `Main/Features/HostilePeace/Models/TaomTradeAgreementModel.cs` | Block B |
| `Main/Features/HostilePeace/Models/TaomSettlementAccessModel.cs`, `HostilePeaceAccess.cs` | Block C |
| `Main/Features/HostilePeace/HostilePeaceTexts.cs` | The refusal texts |
| `Main/_Module/ModuleData/diplomacy/hostile_peace.json` | The JSON switches |

## Testing

- `TAOM.Tests/Features/HostilePeace/HostilePeaceServiceTests.cs`: each block for a Hostile pair at peace, the same pair at war,
  a non-Hostile pair, a switch off, blank and identical ids, the player's own settlement, and the crime cap (leader or not, war,
  switch off, Neutral and identical realms).
- `TAOM.Tests/Features/HostilePeace/HostilePeaceWiringTests.cs`: the module and its patch binding, the models' overrides, the
  access answers against the base game's war answers (town, castle, keep, village), MCM over JSON, the JSON defaults and the
  shipped file.

## How to verify in game

Not done yet. Start a campaign as Thranduil (Woodland Realm) and stay before day 30:
1. A Mirkwood caravan's route never ends in a Dol Guldur town (follow one, or read the caravan's target in its tooltip).
2. Kingdom screen, Diplomacy: a trade agreement with Dol Guldur is refused with "Your realms are enemies in all but war. They
   will not trade."; one with Lothlorien is offered as before.
3. Ride to a Dol Guldur town: the gates are shut and only "Disguise yourself" is offered. A Dol Guldur castle stays closed. In a
   Dol Guldur village, Trade, Recruit and Wait are greyed with the base game's hostile-village texts. A Lothlorien town opens.
4. Found a kingdom of Gondor culture: the "I propose an alliance" line is missing with Dol Guldur's ruler and present with
   Lothlorien's; the Diplomacy screen refuses the Dol Guldur alliance with "Your realms stand on opposite sides of the war and
   cannot be allied.". Turn "Allow Player Alliances With Anyone" on: both are offered.
5. At a Dol Guldur castle as a clan tier 3 player: Approach the gates, then "Request entry to the castle" is refused (entry
   denied), not granted.
6. Ride into a Dol Guldur village with a Provender-master hired: he does not buy there (the base game refuses Trade in that
   village), and he still buys in a Lothlorien village.
7. As a realm ruler, fail sneaks at a Dol Guldur town until the crime rating stops rising: it stops at 59, no war is declared and
   no further relation loss with their leader follows. As a non-ruler (a clan in another realm), crime rises as in the base game.
8. Turn each A to C switch off in MCM ("World/Hostile Peace"): its block lifts at once.

## Known limits

- A caravan already in or heading into a Hostile town when block A turns on trades there once: `OnSettlementEntered` sells and
  the hourly tick buys without asking `CanTradeWith`.
- Block C covers the player only. AI lords already keep out of the other side's recruits and tournaments (Alignment
  Recruitment, Tournament Alignment Filter).
- A trade agreement signed in an old save before this feature runs to its end date.
- A player without a kingdom, or one leading a kingdom missing from `diplomacy.json`, is in no Hostile pair and is not blocked.
