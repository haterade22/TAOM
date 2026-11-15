# Hostile Peace

## Overview

Free and Dark Power realms stay at peace until the War of the Ring, but they do not act as friends. Two realms whose
`diplomacy.json` tier is `Hostile` (every Free realm against every Dark Power, 45 pairs, plus a pledged realm through War
Pledge) get three blocks while they are at peace:

| Block | What stops | Where |
|---|---|---|
| A, caravans | Their caravans do not trade in each other's towns | `Patch156_CaravanTrade_HostilePeace`, a postfix on `CaravansCampaignBehavior.CanTradeWith` |
| B, trade agreements | They never sign a trade agreement | `TaomTradeAgreementModel.CanMakeTradeAgreement` |
| C, the player's access | The player meets their towns and castles as at war: a town only in disguise, a castle not at all, and no trade, recruits or rest in their villages | `TaomSettlementAccessModel` |
| D, the player's alliances | A kingdom the player rules does not ally across the Free/Evil line (any phase, peace or war) | `PlayerAllianceGate`, asked by `TaomAllianceModel`, `TaomKingdomDecisionPermissionModel` and `PlayerAllianceProposalBehavior` |

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
- **Never strands the player.** Block C leaves alone a settlement the player's clan owns and the settlement the player's party
  is in right now; village actions are the exception, refused from inside as at war. The keep and the dungeon keep the base
  rules (reached only from inside a town, where a disguised player is already treated as at war).
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
| `Main/Features/HostilePeace/HostilePeaceService.cs` | The pair rule and the three block questions |
| `Main/Features/HostilePeace/HostilePeaceSettingsProvider.cs` | The three switches, MCM over JSON |
| `Main/Features/HostilePeace/HostilePeaceConfigProvider.cs`, `HostilePeaceConfig.cs` | `hostile_peace.json` |
| `Main/Features/HostilePeace/Hooks/Patch156_CaravanTrade_HostilePeace.cs` | Block A |
| `Main/Features/HostilePeace/Models/TaomTradeAgreementModel.cs` | Block B |
| `Main/Features/HostilePeace/Models/TaomSettlementAccessModel.cs`, `HostilePeaceAccess.cs` | Block C |
| `Main/Features/HostilePeace/HostilePeaceTexts.cs` | The refusal texts |
| `Main/_Module/ModuleData/diplomacy/hostile_peace.json` | The JSON switches |

## Testing

- `TAOM.Tests/Features/HostilePeace/HostilePeaceServiceTests.cs`: each block for a Hostile pair at peace, the same pair at war,
  a non-Hostile pair, a switch off, blank and identical ids, the player's own settlement and the one the player is in.
- `TAOM.Tests/Features/HostilePeace/HostilePeaceAccessTests.cs`: the access answers equal the base game's war answers.
- `TAOM.Tests/Features/HostilePeace/HostilePeaceSettingsTests.cs`: MCM over JSON, the JSON defaults and the shipped file.

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
5. Turn each A to C switch off in MCM ("World/Hostile Peace"): its block lifts at once.

## Known limits

- Block C covers the player only. AI lords already keep out of the other side's recruits and tournaments (Alignment
  Recruitment, Tournament Alignment Filter).
- A trade agreement signed in an old save before this feature runs to its end date.
- A player without a kingdom, or one leading a kingdom missing from `diplomacy.json`, is in no Hostile pair and is not blocked.
