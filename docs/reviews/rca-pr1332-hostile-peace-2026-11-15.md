# RCA: review of PR #1332, Hostile Peace

PR #1332 (REDACTEDSTUFF, head 8af499eb8) adds Hostile Peace: while two Hostile-tier realms are at peace before the War
of the Ring, (A) their caravans do not trade in each other's towns (`Patch156` on `CaravansCampaignBehavior.CanTradeWith`),
(B) they sign no trade agreement (`TaomTradeAgreementModel`), (C) the player meets their settlements as at war
(`TaomSettlementAccessModel`), and (D) a kingdom the player rules does not ally across the Free/Evil line
(`PlayerAllianceGate`). The design is the owner's (2026-11-15). The review ran at tier 1 (a Harmony patch, two
GameModels, a campaign decision path): lens 5 (data flow) and lens 2 (engine compatibility), each at the agent
definition's `max`. Codex did not run: it is out of usage (Mike, 2026-11-15).

Both lenses found blocks A, B and D bound correctly on v1.5.7: the private target and its parameter names, the
co-existence with `Patch59_CaravanTrade`, every trade agreement path, the -10000 alliance score against the `< 50`
gates, the model order (the last `AddModel` of a type wins, and TAOM's models are added after the defaults), the
settings flow from MCM and JSON, and the settings counts. Block C did not work at towns and castles.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Block C's "the player is inside it" exemption was always true at the gate: `PlayerEncounter.Init` calls `EnterSettlement()`, which sets `CurrentSettlement`, before it opens the gate menu that asks the model. Towns and castles were never limited (lens 5 item 4, lens 2 HIGH-1; the session read `PlayerEncounter.cs:706-710`, `EnterSettlementAction.cs:82`, `EncounterGameMenuBehavior.cs:2283`) | Engine order assumed | The unit tests passed `playerIsInside: false` for the gate, so they pinned the assumption | Fixed: the exemption is gone. No town or castle menu asks the model from inside (only gate options read the answer), so nobody is stranded. Lesson appended to `campaign-mechanics.md` |
| 2 | HIGH | A castle's "Request entry to the castle" asks `CanMainHeroEnterLordsHall`, which the PR did not override; at peace it grants a clan tier 3 player full access (lens 5 item 5, lens 2 HIGH-2) | Override set incomplete | The doc assumed the keep is reached only from inside a town, which holds for towns only | Fixed: the model overrides `CanMainHeroEnterLordsHall` for a limited castle with the base game's war answer (`HostilePeaceAccess.KeepAsAtWar`, read from `DefaultSettlementAccessModel.cs:119-126`). Test in `HostilePeaceWiringTests` |
| 3 | MEDIUM | Provender's `RefusesPlayer` asked only the enter check, so the Provender-master bought food in a Hostile village whose Trade block C refuses (lens 5 item 7) | Second consumer of the model | The PR searched for model overrides, not for TAOM readers of the model | Fixed: a village also refuses when the model refuses Trade there |
| 4 | MEDIUM | The API snapshot was not regenerated: no Patch156 row, no rows for the two models (lens 2 MEDIUM-1) | Generator not run | The contributor's run could not judge it | Fixed: regenerated; `snapshot_api_surface.ps1 -Check` reproduces |
| 5 | LOW | Doc comments cited `TradeAgreementDecision.IsAllowed` (it only checks war; `CanMakeDecision` asks the model) and line numbers that differ in the installed DLL (lens 2 LOW-1) | Engine claim from memory | Line numbers move between decompiles | Fixed: the comments and the registry name members |
| 6 | LOW | A caravan already in or heading into a Hostile town when the block turns on trades there once: `OnSettlementEntered` sells and the hourly tick buys without `CanTradeWith` (lens 5 item 2, lens 2 LOW-2) | Effect overstated | | Documented in `hostile-peace.md` "Known limits"; no code change (a one-time effect after a toggle or an old save) |
| 7 | LOW | Each failed sneak into a Hostile town adds crime, and at the crime threshold the engine declares war on a kingdom the player leads, which TAOM lets through; failed sneaks can start a Free/Shadow war before the War of the Ring (lens 2 LOW-3) | Gameplay consequence | The design text covered capture, not the war | Fixed on Mike's word (2026-11-15, 'crime only'): `TaomCrimeModel` caps the crime rating with a Hostile realm at peace one point below the war threshold when the player leads the realm, so no war and no repeated relation loss |
| 8 | LOW | Comments and docs said "three" blocks where there are four; the JSON defaults were literals although the provider claimed they cannot drift; the doc named two test files that do not exist and a pair count nothing computes; the feature-map row left out block D (lens 5 item 17) | Stale wording | Block D was added after the text | Fixed |
| 9 | LOW | Under co-op, block D reads the local `Clan.PlayerClan`, so on the host a guest-ruled kingdom gets the AI rule (lens 5 item 9) | Single-player assumption | Same as the old player bonus | Not applied. FOLLOW-UP no issue: co-op diplomacy for guests is a wider design question for Mike |

## Why each lens caught or missed these

- Both lenses found rows 1 and 2 on their own, which makes them the most certain; the session confirmed row 1 in the engine source.
- Lens 2 alone found rows 4, 5 and 7: it reads the binding gates, the decision class and the crime chain.
- Lens 5 alone found rows 3, 8 and 9 by tracing every reader of the access model and every text against the code.

## In-game checks owed

`hostile-peace.md` "How to verify in game": a Dol Guldur town (disguise only) and castle (gate and Request entry
refused) at peace as Thranduil, a Hostile village's Trade refused with Provender not buying there, and a Free kingdom
the player rules refused an alliance with Mordor.
