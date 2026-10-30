# Diplomacy

## Overview
Encodes LOTR lore-based relationships between kingdoms (Permanent alliance, Natural alliance, Neutral, Hostile), enforces those relationships at runtime by blocking alliance dissolution and war declarations between permanently-allied kingdoms, and drives a scripted "War of the Ring" escalation that triggers wars between factions on a configurable day schedule.

## Why This Exists
- **Vanilla behavior:** Bannerlord's AI makes alliance and war decisions based solely on power scores, relations, and proximity. Over time kingdoms freely ally with or declare war on any faction including their lore enemies.
- **TAOM requirement:** LOTR factions have fixed alignment (e.g., Gondor and Rohan are permanently allied; Mordor, Gundabad, Isengard, and Dol Guldur are permanently allied). These alliances must not dissolve. Additionally, the War of the Ring (Isengard attacking Rohan, then the full conflict) must trigger on schedule regardless of AI decisions.
- **Without this feature:** Mordor might ally with Gondor; the War of the Ring would never start; permanent lore alliances would be broken by AI diplomacy within days.

## Architecture

### Design Challenge
Two problems exist:

1. **Permanent alliances** — `AllianceCampaignBehavior.EndAlliance` and `DeclareWarAction.ApplyInternal` are sealed TaleWorlds methods. They cannot be overridden; Harmony Prefix patches are needed to block execution when the affected kingdoms are permanently allied.
2. **War of the Ring** — The scripted war must trigger at a configurable day and must respect an MCM override. The AI must be prevented from making peace between hostile-tier kingdoms once the full war is active.

### Solution Approach

**Diplomacy subsystem:**
- `DiplomacyService` (implements `IDiplomacyService`) loads `diplomacy.json` at startup via `IDiplomacyConfigProvider` and builds an in-memory dictionary of `(kingdomA, kingdomB) -> AllianceTier` using a canonical key (alphabetical order).
- `AllianceActionHook` (implements `IOnAllianceAction`) consults `DiplomacyService` to decide whether to block alliance-end and war-declaration events.
- `Patch11_Diplomacy` uses Harmony Prefix patches on `AllianceCampaignBehavior.EndAlliance`, `DeclareWarAction.ApplyInternal` and `AllianceCampaignBehavior.StartCallToWarAgreement` and `OnCallToWarAgreementProposedByPlayerKingdom`, plus `IsAllowed` on `ProposeCallToWarAgreementDecision` and `AcceptCallToWarAgreementDecision`, delegating to `IOnAllianceAction`. The war prefixes ask `IWarOfTheRingService.GetWarVeto`, which checks Permanent first, then `WarEnded`, then the same-alignment rule, and only then the phase rule ([war-of-the-ring.md](war-of-the-ring.md)); a same-side pair therefore reads the alignment text in every phase before `WarEnded`, and the phase text reaches only opposed or neutral pairs. The `DeclareWarAction` prefix reads `declareWarDetail`: a player-caused war (`CausedByPlayerHostility`, `CausedByCrimeRatingChange`, `CausedByKingdomCreation`) skips the two phase vetoes only. `TaomDiplomacyModel.GetScoreOfDeclaringWar` subtracts a 10,000,000 (phase veto) or 100,000,000 (Permanent, same-alignment) penalty so the AI does not propose a war the rule would cancel, and `TaomKingdomDecisionPermissionModel` takes its refusal text from `AllianceActionHook.GetWarRefusalText`.
- `TaomAllianceModel` overrides `DefaultAllianceModel.GetScoreOfStartingAlliance` to add a score modifier (+1000 for Permanent, +500 for Natural, -10000 for Hostile) so the AI naturally favors or avoids alliances matching lore.
- `DiplomacyBehavior` (`CampaignBehaviorBase`) establishes initial permanent alliances on new game creation and re-enforces them on every session load.

**Player Alliance Freedom subsystem:**

A player who founds their own kingdom could not form alliances. This is **not** a TAOM block — it is two vanilla limitations: (1) the player can never *initiate* an alliance (`KingdomDecisionProposalBehavior.DailyTickClan` early-returns for `Clan.PlayerClan`; `AllianceCampaignBehavior` only delivers offers *to* the player), and (2) a new player kingdom cannot clear `DefaultAllianceModel.CanMakeAlliance`'s `>= 50f` acceptance score (vanilla scores 0 without bordering fiefs + a >430-threat neighbor), so AI never offers and the v1.4.6 Kingdom→Diplomacy "Propose/Enact Alliance" button stays greyed.

- **Receive + unblock the vanilla button (Part A).** `IDiplomacyService` exposes player-aware overloads: `GetAllianceScoreModifier(a, b, involvesPlayer)` returns `+1000` when the player's kingdom is one of the pair (clears the 50f wall, skips the `-10000` Hostile penalty), and `IsAllianceDecisionAllowed(a, b, involvesPlayer)` returns true for any player-involved pair. `TaomAllianceModel` and `TaomKingdomDecisionPermissionModel` compute `involvesPlayer = querier == Clan.PlayerClan?.Kingdom || queried == Clan.PlayerClan?.Kingdom` at the boundary and delegate. The v1.4.6 Kingdom-screen button's enable-gate routes through `StartAllianceDecision.IsAllowed()` (→ `IsStartAllianceDecisionAllowedBetweenKingdoms` → the permission model) and `CanMakeDecision()` (→ `CanMakeAlliance` → the alliance score), so Part A turns the *existing vanilla button* on. **No custom UI was built** — the vanilla button already exists; a UIExtenderEx duplicate would only add crash surface.
- **Initiate via dialog (Part B).** `PlayerAllianceProposalBehavior` (`CampaignBehaviorBase`) registers a conversation line — "I propose an alliance between our realms." — that appears when a player who *rules* their kingdom talks one-on-one to another kingdom's *ruler*. On accept it forms the alliance via `IAllianceAdapter.StartAlliance`. Gated by `CanPlayerProposeAlliance` (ids present + distinct, at peace, not already allied). Stateless (alliances persist via vanilla's `AllianceCampaignBehavior`).
- **Full freedom (design decision):** player-involved pairs ignore the lore `Hostile` tier — a Gondor-culture player kingdom may ally Mordor. AI-vs-AI diplomacy is unchanged: the retained 2-arg `GetAllianceScoreModifier`/`IsAllianceAllowed` paths behave exactly as before, and `involvesPlayer:false` is byte-identical to the legacy behavior.
- **Cost asymmetry (intentional):** the dialog forms the alliance directly at **0 influence**; the vanilla Kingdom-screen button uses the influence-cost `StartAllianceDecision` flow (~200 influence for a multi-clan kingdom). Two deliberate paths.
- `MaxNumberOfAlliances => int.MaxValue` (pre-existing on `TaomAllianceModel`) removes the vanilla cap of 2 — model-global, so it also lets AI kingdoms exceed 2 alliances.
- **Durability (2026-06-17 follow-up — UNDER INVESTIGATION, diagnostics only).** A formed player alliance was reported vanishing from the encyclopedia. Confirmed vanilla mechanism: `AllianceCampaignBehavior.OnWarDeclared` (AllianceCampaignBehavior.cs:678-681) calls `EndAlliance` the instant war is declared between two allied kingdoms, and TAOM's `AllianceCampaignBehavior_EndAlliance_Patch` protects **only `Permanent`-tier** pairs — a player alliance is `Neutral` → unprotected. But the *trigger* (what declares the war) was never reproduced, so form-then-broken vs never-persists is **unconfirmed**. A first-pass fix (a `DiplomacyService.IsWarAllowed` branch blocking war between the player's ruled kingdom and a current ally) was **reverted** — `/review-codex` showed it soft-locks the player: v1.4.6 has **no "break alliance" UI**, so the player's only exit from an alliance is to *declare war on the ally* (`KingdomDiplomacyVM` → `DeclareWarDecision` → `DeclareWarAction.ApplyByKingdomDecision`), which the block prevented at both the permission model and the `DeclareWarAction` prefix. `IsWarAllowed` is back to its prior behavior (Permanent + same-alignment only). Next step is diagnostics-first (below), then a targeted fix against the confirmed cause.
- **Diagnostics (TEMPORARY — strip after in-game sign-off).** `AllianceCampaignBehavior_StartAlliance_Patch` (Postfix, `Patch11_Diplomacy`) logs `[Diplomacy][diag] Player alliance FORMED` on any path (kingdom-screen button + dialog); the `EndAlliance` patch logs `[Diplomacy][diag] Player alliance END attempt`. Together they distinguish **form-then-break** (FORMED then END attempt — and what war triggered it) from **never-persist** (FORMED never logs). These are the only behavior in the 2026-06-17 follow-up that ships; they make no gameplay change. Remove once the root cause is confirmed in-game and the real fix lands (per `feedback_comprehensive_diag_logging_then_remove`).

**War of the Ring subsystem:**
- `WarOfTheRingService` (implements `IWarOfTheRingService`) tracks `CurrentPhase` (`Peace`, `IsengardWar`, `FullWar`, and the terminal `WarEnded` that `EndWar` sets when a momentum victory fires). Each daily tick it checks elapsed campaign days against configured thresholds and transitions phases.
- Phase 1 (`IsengardWar`): declares specific wars from `phase1.wars` in `war_of_the_ring.json`.
- Phase 2 (`FullWar`): the phase turns first, then it declares the `phase2.wars` list and, if `autoWarBetweenHostileTiers` is true, every hostile-tier pair whose stored stance is not already War (`IAllianceAdapter.HasDeclaredWar`, not the model-aware `AreAtWar`, which reports every Hostile pair at war in Full War; #772). Each host session launch re-runs those declarations through `ReconcileDeclaredWars`.
- `PeaceActionHook` (implements `IOnPeaceAction`) blocks `MakePeaceAction.ApplyInternal` when `FullWar` is active and the two factions are in a hostile-tier relationship.
- Two edges are decided: a vassal who enlists under a lord of another kingdom during days 30 to 44 has the enlistment war mirror declare that lord's wars for the player's liege kingdom as a player-caused war, which can pull the liege into the Rohan war early ([enlistment.md](enlistment.md)); and the lord-dialogue "sponsor a call to war" option is not silent (a known gap): vanilla's call-to-war cost (`DefaultAllianceModel.GetCallToWarCostForCalledKingdom`) subtracts `GetScoreOfDeclaringWar`, so for a vetoed pair the option shows a cost in the millions and is greyed out, and only an ally with no fiefs or a player holding millions reaches the `StartCallToWarAgreement` backstop, which then does nothing.
- Phase day thresholds are overridable via MCM (`ITaomSettingsProvider` wraps `TaomSettings`); a test-mode with short day counts is available in the config.

### Component Diagram
```
CampaignEvents.OnNewGameCreatedPartialFollowUpEvent
CampaignEvents.OnSessionLaunchedEvent
        |
DiplomacyBehavior --> IDiplomacyService.EstablishInitialAlliances
                      IDiplomacyService.EnforcePermanentAlliances
                               |
                         IAllianceAdapter (StartAlliance, AreAllied)

CampaignEvents.DailyTickEvent, CampaignEvents.OnSessionLaunchedEvent (host only)
        |
WarOfTheRingBehavior --> IWarOfTheRingService.CheckPhaseTransition (both events)
                     --> IWarOfTheRingService.ReconcileDeclaredWars (session launch only, runs only in FullWar, idempotent, #772)
                     --> IWarOfTheRingService.EndWarsForbiddenByPhase (after CheckPhaseTransition, daily tick only, authority only, skips the player kingdom's phase-vetoed wars)
                                  |
                            IAllianceAdapter (HasDeclaredWar, DeclareWar, GetAllKingdomIds)
                            IDiplomacyService.GetRelationshipTier

Harmony Patch11_Diplomacy
  AllianceCampaignBehavior.EndAlliance [Prefix] -> IOnAllianceAction.ShouldPreventAllianceEnd  (+ [diag] log)
  DeclareWarAction.ApplyInternal       [Prefix] -> IOnAllianceAction.ShouldPreventWarDeclaration -> IWarOfTheRingService.GetWarVeto (Permanent first, then WarEnded, then the same-side rule, then the phase rule; a player-caused war skips the phase rule)
  AllianceCampaignBehavior.StartCallToWarAgreement [Prefix] -> IOnAllianceAction.ShouldPreventWarDeclaration (same veto, before any gold moves)
  AllianceCampaignBehavior.OnCallToWarAgreementProposedByPlayerKingdom [Prefix] -> same veto, so a one-clan player kingdom gets no map notice for a refused war
  ProposeCallToWarAgreementDecision.IsAllowed / AcceptCallToWarAgreementDecision.IsAllowed [Prefix] -> same veto, result false, so vanilla cancels the ballot before the player sees it
  MakePeaceAction.ApplyInternal        [Prefix] -> IOnPeaceAction.ShouldPreventPeace
  AllianceCampaignBehavior.StartAlliance [Postfix] -> [diag] log (TEMPORARY, player-involved)

GameModel: TaomAllianceModel : DefaultAllianceModel
  GetScoreOfStartingAlliance -> IDiplomacyService.GetAllianceScoreModifier(a,b,involvesPlayer)

GameModel: TaomKingdomDecisionPermissionModel : DefaultKingdomDecisionPermissionModel
  IsStartAllianceDecisionAllowedBetweenKingdoms -> IDiplomacyService.IsAllianceDecisionAllowed(a,b,involvesPlayer)

Conversation dialog (initiate)
  PlayerAllianceProposalBehavior --> IDiplomacyService.CanPlayerProposeAlliance
                                     IDiplomacyService.FormPlayerAlliance
                                              |
                                        IAllianceAdapter.StartAlliance

Vanilla Kingdom->Diplomacy "Propose/Enact Alliance" button (receive/initiate)
  StartAllianceDecision.IsAllowed      -> TaomKingdomDecisionPermissionModel (player bypass)
  StartAllianceDecision.CanMakeDecision -> TaomAllianceModel score (+1000 player bonus clears 50f gate)
```

### Kingdom vote deadlock guard (`Patch80`)

Vanilla can open a kingdom decision window that afterwards cannot be closed by any means, which
players report as a frozen game. The window's normal exit is a five-second timer inside
`KingdomDecisionPopupWidget` that starts only when `IsKingsDecisionOver` flips true, and that flip
only happens if the election concludes. On a cancelled election `KingdomElection.ApplySelection()` is
a silent no-op, so the flip never comes, the timer never starts, and `ExecuteFinalSelection` has
already disabled the popup's only button. Full mechanism, every call site and the reasoning behind
each seam: [`harmony-patch-registry.md` § Patch80_KingdomVoteDeadlock](../reference/harmony-patch-registry.md).

TAOM does not cause it but does raise how often it fires. `TaomAllianceModel.MaxNumberOfAlliances`
is `int.MaxValue`, and vanilla `AllianceCampaignBehavior.OnWarDeclared` queues one
`ProposeCallToWarAgreementDecision` per allied kingdom, so a single war declaration can produce a run
of back-to-back ballots. Each yes vote declares its war synchronously, which can invalidate the next
ballot in the same run. **If the number of votes is itself the complaint, the alliance cap is the
knob, not this guard.**

What a vote costs on the campaign map, measured by yotthani (MithrilForge `docs/engine/perf.md`@0a2f4761, in game,
v1.5.3; v1.5.5: not re-checked; the TAOM version loaded is not stated):

- **An election is cheap once ButterLib's rebuild is gone.** With the `butterlib-lazy-distance` patch on, an election
  averaged 3.7 ms over 39 `KingdomElection.StartElection` calls; the 110 to 130 ms elections seen before were mostly
  ButterLib's handler on the owner change (lines 1280-1282, runs of 2026-10-04). TAOM removes that handler's cost by
  switching the subsystem off ([butter-lib-distance-matrix.md](butter-lib-distance-matrix.md)).
- **A war declaration with alliances costs 17 to 35 ms a call**, and the outliers that remain are many rare events of
  20 to 60 ms each (election plus declaration, battle simulation, single settlements' daily ticks), with no single hot
  function (lines 1122 and 1143, MithrilForge `docs/perf-audit/k3-periodic-ticks.md`, 2026-10-03).
- **Unlimited alliances multiply the call-to-war ballots.** yotthani's code reading, not a measurement: the
  war and peace evaluation runs about 8 x N times in every election, mostly with the same arguments, and TAOM's
  unlimited alliances multiply the call-to-war elections (lines 1124-1126).
- **A war-score cache was rejected.** A shadow test found 78 % hits and no deviation, but `GetScoreOfDeclaringWar`
  costs only 12 us a call, a saving of about 7 ms in four minutes; the patch stays off (lines 1141-1142).

```
KingdomDecisionsVM.RefreshWith           -> Prefix: a stale ballot never builds a window
KingdomDecisionsVM.HandleDecision        -> Postfix: re-arm the queue vanilla left switched off
DecisionItemBaseVM.ExecuteFinalSelection -> Postfix: force-close a cancelled election's window
                                            (via _onDecisionOver, never ExecuteDone)
        all three -> IKingdomVoteDeadlockService.ShouldSuppressBallot / AnnounceLapsedBallot
                  -> IKingdomBallotAdapter (ADR-007 boundary over KingdomDecision)
KingdomDecisionsVM.RefreshWith           -> Postfix (seam D, #550): a window whose election
                                            concluded inside the view model's constructor is
                                            closed at once through vanilla ExecuteDone
DecisionItemBaseVM.ExecuteDone           -> Prefix (seam E, #550): runs only on an active window
                                            whose election is over, so the popup widget's timer,
                                            which outlives a seam D close, cannot fire it twice
```

**Seam D, the pre-concluded election (2026-09-15, #550).** A second mechanism produces the same
stuck window and none of the three seams above can see it. When the player is not a supporter of
the election (`Supporter.IsPlayer => Clan.Leader.IsHumanPlayerCharacter`, false whenever
`Clan.PlayerClan.Leader != Hero.MainHero`), `KingdomElection.StartElection()` takes
`ReadyToAiChoose()`, which applies the outcome and fires `KingdomDecisionConcluded` synchronously,
inside `DecisionItemBaseVM`'s own constructor. The view model's own handler sets
`IsKingsDecisionOver = true` before it is bound; `IsCancelled` stays false. The popup widget's
`IsKingsDecisionDone` latch is edge-triggered, reused across decisions and never reset, so the
first such window closes on the five-second timer and every later one in the same screen visit
binds already-true, gets no edge, and stays open. `KingdomDecisionsVM_RefreshWith_AutoResolved_Patch`
postfixes `RefreshWith`, matches `CurrentDecision` to the ballot this call built, and when
`IsActive && IsKingsDecisionOver` already hold, runs vanilla's own `ExecuteDone` through the cached
`MethodInfo` in `KingdomVoteDeadlockBinding`. `ExecuteDone` is exactly right here and exactly wrong
on seam B: it opens with `GetChosenOutcomeText()` on `_chosenOutcome`, which `ReadyToAiChoose()`
always assigns and a cancelled election never does. It hides the popup, shows the outcome, clears
the concluded listener and runs `OnDecisionOver`; nothing is re-implemented. A hit is logged at
warning level because TAOM's own route into this state, the Player Switcher's takeover of a king's
spouse or child, is closed at the source (the takeover now promotes the lord to clan leader, and
older saves are repaired at session launch, see [player-switcher.md](player-switcher.md)); a hit
means some other route left the player outside their clan's leadership.

**Seam E, the timer that outlives the close (Codex review 113, F1).** The bind pushes
`IsKingsDecisionOver` into `KingdomDecisionPopupWidget`, whose latch arms its five-second timer,
and only that timer's own `ExecuteFinalDone` ever disarms it. Seam D closes before it fires, so
five seconds later the widget's `FinalDone` reaches whatever item is bound at the time: the same
closed item (a duplicate inquiry, which `GauntletQueryManager.CreateQuery` rejects because the
outcome lambda captures only `this` and the two delegates compare equal) or, if the player opened
the next decision inside that window, a live item whose `_chosenOutcome` is null, where
`GetChosenOutcomeText()` NREs inside `OnLateUpdate`. `DecisionItemBaseVM_ExecuteDone_Patch`
prefixes `ExecuteDone` and lets vanilla run only when `IsActive && IsKingsDecisionOver`, which every
legitimate call already satisfies (the timer arms only on the true edge; nothing but `ExecuteDone`
clears `IsActive`). A skipped call is a debug line and is expected once per seam D close.

### Free/Shadow side boundary: yotthani's report and what TAOM decided (2026-10-29)

yotthani (repo `yotthani/bannerlord`) wrote `docs/taom/side-boundary-report.md`@6265f4a2 (95 lines). It states it was
"Checked against TAOM `bannerlord-1.5.x` at v2.0.35 (`bd1ac8292`)", with numbers from "29 unattended CampaignProbe
campaigns (TAOM 2.0.34, 150 days each)" (lines 3-5). The report names no game version; the CampaignProbe docs of the same commit say 1.5.5 (`taom-baseline-2026-10-18.md`:10; that the same runs are meant is TAOM's reading). Its sides are TAOM's own `alignment.json`. It names
three gaps. TAOM is now Bannerlord 1.5.6 and v2.0.36; each gap below was re-checked in the TAOM tree at 0c5d81408.

**Gap 1: peace across the line before the War of the Ring** (report lines 13-29).
- yotthani's measurement: "134 peace treaties between a Free and a Shadow realm in 29 campaigns, mostly between day 20 and
  day 44", and one pair (bluecraig and lindon) "keeps making peace even after day 44". The cause given: `ShouldBlockPeace` "returns
  true only in the FullWar phase, and only for pairs of Hostile tier".
- TAOM [Certain, code reading at 0c5d81408]: `WarOfTheRingService.ShouldBlockPeace` (`WarOfTheRingService.cs`:74-80) returns
  false unless the phase is `FullWar` and `blockPeaceBetweenHostileTiers` is set, then true only for the `Hostile` tier. Its
  readers are `PeaceActionHook.cs`:33, `TaomKingdomDecisionPermissionModel.cs`:74 and `TaomDiplomacyModel.IsAtConstantWar`
  (`:39`). No TAOM patch touches a barterable or `MakePeaceKingdomDecision.IsAllowed` (grep of `Main/` and `Client/`).
- **The numbers probably predate 2410aec51** [Likely, the verifier's inference]. Since that commit (v2.0.34, 2026-10-18) the
  campaign opens at peace: every start relationship is `value="0" isAtWar="false"` (`spkingdoms.xslt`:57-62, 181-186), and AI
  wars are vetoed until phase 1 on day 30 (`war_of_the_ring.json`). Peaces "mostly between day 20 and day 44" cannot come
  from those rules, so those runs most likely used a 2.0.34 build from before the commit. yotthani's own code check used `bd1ac8292`,
  which does contain it (`git merge-base --is-ancestor` true). Do not read the 134 as a rate for 2.0.36. yotthani's suggestion to
  lock peace from day 1 would also meet no starting wars to freeze.
- **Engine qualification** [Certain, v1.5.6 decompile; scope: the partial v1.5.6 cache]. The report says AI peace offers go
  "through barter (`PeaceBarterable`)". In v1.5.6 the only AI construction of a `PeaceBarterable` is `ConsiderPeace`
  (`DiplomaticBartersBehavior.cs`:223-229), called from line 54 for a clan with `Kingdom == null` against another kingdomless,
  non-minor clan. A kingdomless clan's peace with a kingdom is a direct `MakePeaceAction.Apply` (`:73`). The other
  construction site is the player's conversation (`LordConversationsCampaignBehavior.cs`:2719). Realm against realm peace is
  therefore a `MakePeaceKingdomDecision` plus `MakePeaceAction`, not a barter, and a barterable patch would reach only
  independent clans and the player. The seams that matter for realms are the ones TAOM's three readers already use.

**Gap 2: mercenary bands change sides** (report lines 31-44).
- yotthani's measurement: "233 mercenary realm changes across the Free/Shadow line in 29 campaigns", and "97 % of 1373
  cross-side town visits came from such bands". yotthani's data note: a clan row in `alignment.json` "only counts while the band
  serves no kingdom (`FactionSides.OfHero` uses `MapFaction`)".
- TAOM [Certain, 0c5d81408]: no side rule touches `JoinAsMercenary`, `MercenaryJoinKingdomBarterable` or
  `ChangeKingdomAction` (grep of `Main/` and `Client/` C#: `KingdomJoinAdapter.cs`:64 joins the player clan to a kingdom for Player Switcher, the other hits are clan-changed-kingdom listeners in SwornAnew, Namegiving and three LotrIssues templates and comments in `BondOfHireService.cs`:10 and `SwornAnewService.cs`:54, and none tests a side). A band under contract resolves to its employer's side ([wanderer-allegiance.md](wanderer-allegiance.md) "Bands under contract take their employer's side"). `AlignmentDesertionService.cs`:73-74 already records the same effect. The intended band table the report points at is
  `TAOM.Tests/Features/Execution/ShippedClanSideCoverageTests.cs`:43-56.
- **A model seam without Harmony** [Certain for the call, side effects UNVERIFIED]. Before a minor clan considers a mercenary
  contract, v1.5.6 asks `DiplomacyModel.IsAtConstantWar(clan, kingdom)` and returns on true
  (`DiplomaticBartersBehavior.cs`:117; the minor-clan branch is `:135-137`, the barterable is built at `:197-199`; the
  defection branch checks it too at `:94`). TAOM already overrides that model (`TaomDiplomacyModel.cs`:11, 35-44; registered at
  `SubModule.cs`:1145). Not known: whether answering true also makes the pair fight. `FactionHelper.cs`:390
  (`AdjustFactionStancesForClanJoiningKingdom`) and `LordConversationsCampaignBehavior.cs`:2686 read it as well, and
  `FactionManager.IsAtConstantWarAgainstFaction` is not in the cache. Read those before using the seam.

**Gap 3: wanderers spawn anywhere.** See [wanderer-allegiance.md](wanderer-allegiance.md) "Where a wanderer stands". TAOM
places no wanderer by side [Certain: no `TeleportHeroAction` or `EnterSettlementAction` in the wanderer feature at 0c5d81408].

**Peace hazard by pair class** (measured by yotthani in 22 pure-TAOM runs of 2.0.34, wars declared from day 7;
`docs/balance-sim/campaign.md`@6265f4a2:1103-1109). Neutral against neutral: 22 peaces in 3,736 war-days at risk, 0.0059 a day
(52 wars, mean length 72 days). Evil against neutral: 42 in 1,092, 0.0385 a day (44 wars, 25 days). Every other non-Hostile
pair keeps the source model's flat 0.042. Shaghana against Umbar is at war 88.6 days a run (`:1097`). The source's hedge, word for word: the
split is "a class split of a fitted process and not a rule of the game (the game decides by war score and a vote,
`KingdomDecisionProposalBehavior.cs:256-345`)" (`:1099`). "From day 7 on" is the source's counting window, not a statement about the build. TAOM's inference [Likely]: these runs also predate 2410aec51, because the same 22 runs show the Shaghana and Umbar war declared at a median day 17 (`:1097`) and since that commit no AI war starts before day 30; the source does not state the build of its 14 runs of 21 October. The 2.0.36
alignment table lists six more Neutral realms (cardolan, rhudaur, anduinvale, angaladh, dorwinion, southrhovanion; read in
`alignment.json` at 0c5d81408), so neutral against neutral wars may become more common (TAOM's inference); the 72-day figure
is the only measured length for such wars.

**Coexistence** (report lines 75-95). yotthani's optional module VanillaTuning_TAOM carries all three rules behind the
switches `side-peace-lock`, `side-mercenary` and `side-wanderer`. It states "If TAOM adopts any of these rules,
we switch ours off", and that both modules prefix `MakePeaceAction.ApplyInternal` and "both only block, so the two prefixes
combine without conflict". TAOM has not checked whether a later prefix still runs after an earlier one returns false.

**Decision, 2026-10-29: no code change now.** All three gaps are real in TAOM's code, but they are design gaps, not defects,
and the evidence for the first is stale. A change needs fresh evidence from 2.0.36 campaigns (a Free/Shadow peace count with
2410aec51 in the build, and the mercenary and wanderer counts measured there). If the boundary is later meant to hold from
day 1, widen `ShouldBlockPeace` at its existing readers; no barterable patch is needed. A mercenary rule changes the AI
economy of the bands and needs the maintainer's call first.

### Personal relations (`TaomDiplomacyModel`, #862)

`TaomDiplomacyModel` also carries three overrides for [personal relations](personal-relations.md), all asking `IPersonalRelationService` (the model's fourth constructor argument, built in `SubModule.OnGameStart`). They act only in a single player campaign whose save is seeded, while the MCM switch is on; otherwise every one is vanilla.

- `GetHeroesForEffectiveRelation` calls `base`, then, when the service is active and one effective hero is the player, puts the OTHER effective hero back to the hero it was asked about, so a relation lands on the lord dealt with instead of his clan leader. AI-to-AI pairs stay vanilla.
- `GetRelationCostOfDisbandingArmy` returns -(28 to 32) for an army's leader, so the player loses 28 to 32 relation with him (vanilla loses 4). A member keeps vanilla's -1.
- `GetRelationChangeAfterClanLeaderIsDead` returns 0 for the player when a clan's leader changes (death, a leader changing clan, a member elected king), so an heir keeps the relation he earned himself, instead of inheriting 0.7 of the dead leader's.

## Configuration

### `Main/_Module/ModuleData/diplomacy/diplomacy.json`
Defines all kingdom pair relationships. Each entry has `kingdomA`, `kingdomB`, and `tier` (`Permanent`, `Natural`, `Neutral`, or `Hostile`). Keys are matched order-insensitively.

Current data: 38 Permanent, 29 Natural, 7 Neutral and 79 Hostile pairs <!-- measured: python collections.Counter(r['tier'] for r in diplomacy.json relationships) 2026-10-27 -->. Every Hostile pair goes to war at Full War, so a free realm needs a Hostile row with each evil realm; `DiplomacyShippedConfigTests` holds Arthedain and Ered Luin to that. Ered Luin has Arthedain's 9 Hostile rows and is Natural with `erebor` and `lindon`.

### `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json`
Controls the scripted war escalation:
```json
{
  "enabled": true,
  "phase1": { "triggerDay": 30, "wars": [{"attacker":"isengard","defender":"vlandia"}, ...] },
  "phase2": { "triggerDay": 44, "autoWarBetweenHostileTiers": true, "blockPeaceBetweenHostileTiers": true, "wars": [] },
  "testMode": { "enabled": false, "phase1Day": 1, "phase2Day": 3 }
}
```
Phase 1 (Isengard and Dunland attack Rohan) triggers on day 30; Phase 2 (the full War of the Ring) on day 44. MCM overrides these at runtime through `WarPhase1TriggerDay` and `WarPhase2TriggerDay` (renamed in #1017 so the 30 and 44 reach saved settings) and carries the same defaults.

### MCM Settings (`TaomSettings`)
| Setting | Default | Description |
|---------|---------|-------------|
| Enable War of the Ring | `true` | Master switch for phase transitions |
| Phase 1 Start Day | `30` | MCM override for Phase 1 trigger (Isengard attacks Rohan) |
| Phase 2 Start Day | `44` | MCM override for Phase 2 trigger (full war) |

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/Diplomacy/DiplomacyIoC.cs` | DryIoc registrations and static `InitializeHooks` wiring |
| `Main/Features/Diplomacy/DiplomacyBehavior.cs` | `CampaignBehaviorBase`: establishes/enforces permanent alliances on new game and session load |
| `Main/Features/Diplomacy/WarOfTheRingBehavior.cs` | `CampaignBehaviorBase`: daily tick drives phase transition checks; its constructor resets the process-lifetime service for each campaign (#764) |
| `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs` | `CampaignBehaviorBase`: conversation dialog letting a player kingdom-ruler initiate an alliance with another ruler |
| `Main/Features/Diplomacy/IDiplomacyService.cs` | Service interface: tier lookup, score modifier, alliance enforcement, player-freedom overloads + proposal methods |
| `Main/Features/Diplomacy/DiplomacyService.cs` | Implementation: loads config, computes scores, enforces alliances, player-freedom score/permission + `CanPlayerProposeAlliance`/`FormPlayerAlliance` |
| `Main/Features/Diplomacy/IWarOfTheRingService.cs` | Service interface: phase state, peace blocking, phase transition |
| `Main/Features/Diplomacy/WarOfTheRingService.cs` | Implementation: phase state machine, war declarations |
| `Main/Features/Diplomacy/DiplomacyConfigProvider.cs` | Deserializes `diplomacy.json` |
| `Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs` | Deserializes `war_of_the_ring.json` |
| `Main/Features/Diplomacy/TaomSettingsProvider.cs` | Wraps `TaomSettings` MCM for testable access |
| `Main/Features/Diplomacy/Hooks/AllianceActionHook.cs` | `IOnAllianceAction` — decides whether to block alliance end / war declaration |
| `Main/Features/Diplomacy/Hooks/PeaceActionHook.cs` | `IOnPeaceAction` — decides whether to block peace during full war |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_EndAlliance_Patch.cs` | Harmony Prefix — calls `IOnAllianceAction.ShouldPreventAllianceEnd` (+ temporary `[diag]` log of player-involved end attempts) |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_StartAlliance_Patch.cs` | Harmony Postfix — **TEMPORARY diagnostic** (`Patch11_Diplomacy`): logs `[Diplomacy][diag] Player alliance FORMED` on any path (kingdom-screen button + dialog). Strip after in-game durability sign-off. |
| `Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs` | Harmony Prefix: calls `IOnAllianceAction.ShouldPreventWarDeclaration` (→ `GetWarVeto`: Permanent pairs always, then `WarEnded`, then same-alignment pairs, then the War of the Ring phase rule) |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_StartCallToWarAgreement_Patch.cs` | Harmony Prefix: the same veto, so a call-to-war agreement for a refused war moves no gold |
| `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_OnCallToWarAgreementProposedByPlayerKingdom_Patch.cs` | Harmony Prefix: the same veto for a player kingdom's call-to-war notice, which vanilla posts without reading `IsAllowed` |
| `Main/Features/Diplomacy/Hooks/ProposeCallToWarAgreementDecision_IsAllowed_Patch.cs` | Harmony Prefix: the propose decision is not allowed when the war is vetoed |
| `Main/Features/Diplomacy/Hooks/AcceptCallToWarAgreementDecision_IsAllowed_Patch.cs` | Harmony Prefix: the accept decision is not allowed when the war is vetoed |
| `Main/Features/Diplomacy/Hooks/MakePeaceAction_ApplyInternal_Patch.cs` | Harmony Prefix — calls `IOnPeaceAction.ShouldPreventPeace` |
| `Main/Features/Diplomacy/IKingdomVoteDeadlockService.cs` | Service interface: is this ballot stale, and has the player been told |
| `Main/Features/Diplomacy/KingdomVoteDeadlockService.cs` | Implementation: staleness verdict + one announce per ballot, bounded dedupe |
| `Main/Features/Diplomacy/Hooks/KingdomVoteDeadlockBinding.cs` | Cached reflection + the shared queue repair both `KingdomDecisionsVM` seams call |
| `Main/Features/Diplomacy/Hooks/KingdomDecisionsVM_RefreshWith_Patch.cs` | Harmony Prefix (`Patch80`): never build a decision window for a stale ballot |
| `Main/Features/Diplomacy/Hooks/KingdomDecisionsVM_HandleDecision_Patch.cs` | Harmony Postfix (`Patch80`): re-arm the ballot queue vanilla leaves switched off |
| `Main/Features/Diplomacy/Hooks/DecisionItemBaseVM_ExecuteFinalSelection_Patch.cs` | Harmony Postfix (`Patch80`, `Priority.Last`): force-close a window whose election is cancelled |
| `Main/Features/Diplomacy/Hooks/KingdomDecisionsVM_RefreshWith_AutoResolved_Patch.cs` | Harmony Postfix (`Patch80` seam D, `Priority.Last`): close a window whose election concluded inside the view model's constructor, through vanilla `ExecuteDone` (#550) |
| `Main/Features/Diplomacy/Hooks/DecisionItemBaseVM_ExecuteDone_Patch.cs` | Harmony Prefix (`Patch80` seam E): `ExecuteDone` runs only on an active, concluded window, so the widget timer a seam D close leaves armed cannot fire it on a closed or live item (#550) |
| `Main/Adapters/KingdomBallotAdapter.cs` | ADR-007 boundary over one `KingdomDecision` (staleness, identity, title) |
| `Main/Features/Diplomacy/Models/TaomAllianceModel.cs` | `DefaultAllianceModel` override: adds lore score modifier to alliance scoring |
| `Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs` | `DefaultKingdomDecisionPermissionModel` override: blocks lore-Hostile alliance decisions (AI pairs) but allows any decision involving the player's kingdom (full freedom); also blocks war on permanent allies + peace during full War of the Ring |
| `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` | `DefaultDiplomacyModel` override: `IsAtConstantWar` for the peace block, and `GetScoreOfDeclaringWar` minus the veto penalty |
| `Main/Features/Diplomacy/Models/AllianceTier.cs` | Enum: `Permanent`, `Natural`, `Neutral`, `Hostile` |
| `Main/Features/Diplomacy/Models/KingdomRelationship.cs` | POCO: kingdom pair + tier |
| `Main/Features/Diplomacy/Models/DiplomacyConfig.cs` | POCO: list of `KingdomRelationship` |
| `Main/Features/Diplomacy/Models/WarOfTheRingConfig.cs` | POCO: phase configs and test mode |
| `Main/Features/Diplomacy/Models/WarPhase.cs` | Enum: `Peace`, `IsengardWar`, `FullWar`, `WarEnded` (appended; saved as an int) |
| `Main/_Module/ModuleData/diplomacy/diplomacy.json` | Kingdom relationship data |
| `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json` | War of the Ring phase config |

## Dependencies
- `IAllianceAdapter`: wraps `AllianceCampaignBehavior`, `StanceLink`, `Kingdom` (sealed TaleWorlds types); provides `AreAllied`, `StartAlliance`, `AreAtWar`, `HasDeclaredWar` (whether the stored stance link is War; a missing link is created and saved from the model's default stance, so a link-less Hostile pair reads true at Full War, while an existing neutral link reads false though the model reports war), `DeclareWar`, `MakePeace`, `GetAllKingdomIds`, `GetKingdomCultureId`
- `IDiplomacyConfigProvider` — loads diplomacy.json
- `IWarOfTheRingConfigProvider` — loads war_of_the_ring.json
- `ITaomSettingsProvider` — wraps `TaomSettings` MCM
- `IKingdomBallotAdapter`: ADR-007 boundary over one vanilla `KingdomDecision` (staleness, identity, title), constructed at the Patch80 seams rather than resolved from IoC
- `IInquiryAdapter`: the withdrawn-ballot toast; registered by `EnlistmentIoC`, not here (see the ordering note in `DiplomacyIoC`)
- `IModLogger` — logging

## Tests
| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/Diplomacy/DiplomacyServiceTests.cs` | Tier lookup, score modifiers, alliance allowed/blocked, initial alliance establishment, enforcement, player-freedom score/permission overloads, `CanPlayerProposeAlliance` (same/empty/at-war/already-allied/Hostile-tier), `FormPlayerAlliance`, `IsWarAllowed` (Permanent/same-alignment/neutral) |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs` | Phase transitions, peace blocking, MCM override, test-mode day overrides, hostile-tier auto-war, the session reset (#764), the stored-stance guard and `ReconcileDeclaredWars` (#772) |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs` | The constructor reset (#764), the host session launch reconciling before the phase check (#772), the co-op client gate |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs` | SubModule builds `WarOfTheRingBehavior` inline, once (#764) |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingPhaseDayTests.cs` | `GetEffectivePhaseDay` (#765) |
| `TAOM.Tests/Features/Diplomacy/DiplomacyConfigProviderTests.cs` | JSON parsing, missing file handling |
| `TAOM.Tests/Features/Diplomacy/AllianceActionHookTests.cs` | `ShouldPreventAllianceEnd` for permanent vs non-permanent tiers; `ShouldPreventWarDeclaration` follows whatever `GetWarVeto` returns (any veto blocks, the player-caused flag is passed through, co-op defers to the host without asking the service); `GetWarRefusalText` returns the veto's text, null for none, null under co-op, and logs nothing |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingWarVetoTests.cs` | `GetWarVeto` in every phase (side before phase, player-caused wars), the score penalties, `IsEnabled`, and `EndWarsForbiddenByPhase` |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorPhaseWarsTests.cs` | The sweep runs on the daily tick after the phase check, authority only, not on session launch (`RequiresGame`) |
| `TAOM.Tests/Features/Diplomacy/WarVetoReasonsTests.cs` | Every veto has its own keyed inline text; `Permanent` keeps `taom_war_blocked` |
| `TAOM.Tests/Features/Diplomacy/ShippedKingdomDataTests.cs` | No `<relationship>` row in any shipped ModuleData xml or xslt declares war; a `LiveInstall` check of the transformed `spkingdoms.xslt` output |
| `TAOM.Tests/Features/Diplomacy/DeclareWarActionPatchTests.cs` | Which `DeclareWarDetail` values count as player-caused (`RequiresGame`) |
| `TAOM.Tests/Features/Diplomacy/DiplomacyWarPrefixBindingTests.cs` | `BindingVerification`: the war prefixes' parameter names and the decision fields they read, against the installed engine |
| `TAOM.Tests/Features/Diplomacy/WarOfTheRingShippedConfigTests.cs` | The shipped `war_of_the_ring.json`: days, ordering, test mode off, and the exact phase 1 list (`isengard->vlandia`, `empire->vlandia`) |
| `TAOM.Tests/Features/Diplomacy/PeaceActionHookTests.cs` | `ShouldPreventPeace` during active vs inactive War of the Ring |
| `TAOM.Tests/Features/Diplomacy/KingdomVoteDeadlockServiceTests.cs` | Patch80 staleness verdict, null ballot, throwing staleness check (suppresses, does not defer), announce path, per-ballot dedupe, throwing presenter, bounded dedupe set |
| `TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs` | Patch80 engine drift: the three patch targets and their parameter NAMES (Harmony binds by name), the five members reached by reflection (`ExecuteDone` for seam D), the public members the bodies read, `StartElection` still calling `ReadyToAiChoose` (seam D's premise), the category literal on all five seams, `SubModule` applying and initializing them, seam E's target name, and IL call-presence guards for the service calls, the listener clear, the fault-path withdraw, seam D's `ExecuteDone` invoke and seam E's two state reads |

## How to Add a New Kingdom Relationship

1. Open `Main/_Module/ModuleData/diplomacy/diplomacy.json`.
2. Add an entry: `{ "kingdomA": "your_kingdom", "kingdomB": "other_kingdom", "tier": "Permanent" }`. Kingdom IDs must match their `StringId` values in the game (e.g., `empire_w`, `erebor`, `empire_s`).
3. Tier values: `Permanent` (score +1000, alliance enforced, war blocked), `Natural` (score +500, AI likely to ally), `Neutral` (no modifier), `Hostile` (score -10000, alliance blocked, war auto-declared in Phase 2 if `autoWarBetweenHostileTiers` is set).
4. No code changes needed — `DiplomacyService` reads the file at startup.

## How to Add a War of the Ring Phase War

1. Open `Main/_Module/ModuleData/diplomacy/war_of_the_ring.json`.
2. Add a `WarDeclaration` entry to the appropriate phase's `wars` array: `{ "attacker": "empire_s", "defender": "empire_w" }`.
3. The war is declared idempotently: only while the stored stance is not already War (`HasDeclaredWar`, #772). An entry naming an unknown or eliminated kingdom is skipped with one warning per process.

## Changelog

- 2026-10-29: Recorded yotthani's Free/Shadow side-boundary report (three gaps, each confirmed in the code, the peace hazard by pair class, the v1.5.6 mercenary seam) and the decision to change no code until 2.0.36 evidence exists.
- 2026-10-19: Three personal-relations overrides on `TaomDiplomacyModel` (the player's relation stays on the lord named, a larger disband cost for an army's leader, and an heir keeps his own relation), single player only, behind their own MCM group (#862).
- 2026-10-18: Doc pass for the war rule: the side is checked before the phase, `CausedByKingdomCreation` counts as player-caused, a seventh prefix (`OnCallToWarAgreementProposedByPlayerKingdom`) keeps vetoed call-to-war notices off a player kingdom's map, and the enlistment edge and the lord-dialogue sponsor gap are recorded (#818).
- 2026-10-08: A new campaign no longer inherits the previous campaign's War of the Ring phase and outcome: `WarOfTheRingBehavior`'s constructor resets the service (#764).
- 2026-10-18: War rule follow-up: player-caused wars lift the phase vetoes, the AI war score carries the veto penalty, two `IsAllowed` prefixes hide impossible call-to-war offers, and the daily sweep spares the player kingdom's phase-vetoed wars (#818). Open: #819, #820.
- 2026-10-18: War rule by phase (`GetWarVeto`): no war in Peace, only the phase 1 wars in Isengard War, the old rule in Full War. New `StartCallToWarAgreement` prefix; kingdom decision refusals gain three texts. Details in [war-of-the-ring.md](war-of-the-ring.md).
- 2026-10-08: Phase 2 really declares the Hostile-pair wars. The phase turns first and the guard reads the stored stance (`HasDeclaredWar`); at each host session launch, `ReconcileDeclaredWars` declares the wars that a Full War save still stores as neutral (#772).
- 2026-09-15: Patch80 seams D and E (#550). A window built on an election that concluded inside the view model's constructor (the player is not their clan's leader, so `Supporter.IsPlayer` is false) is closed at once through vanilla `ExecuteDone` (D), and `ExecuteDone` itself runs only on an active, concluded window (E, from Codex review 113: the widget timer outlives a seam D close). The four earlier reflection sites plus `ExecuteDone` are now in the reflection-site catalogue. The Player Switcher route into that state is closed at the source in the same change.
- 2026-09-06: `Patch80_KingdomVoteDeadlock` (#547), three seams so a vanilla kingdom decision popup can never become unclosable. Deep review found and fixed two HIGH defects in the first cut (a leaked `KingdomDecisionConcluded` listener, and a fault path that deferred to a vanilla branch which throws); RCA at [rca-kingdom-vote-deadlock-2026-09-06.md](../reviews/rca-kingdom-vote-deadlock-2026-09-06.md). Vote VOLUME is unchanged: `TaomAllianceModel.MaxNumberOfAlliances` is what turns one war declaration into a run of ballots.

- 2026-06-17 — Instrumented player-alliance loss with `[Diplomacy][diag]` logging only; the durability war-block (`DiplomacyService.IsWarAllowed` branch) was reverted after review (it soft-locked the player out of the only alliance-exit path).
- 2026-06-16 — Let player-founded kingdoms form alliances: player-aware service overloads unblock the vanilla Kingdom→Diplomacy button (Part A) and a new `PlayerAllianceProposalBehavior` dialog lets a kingdom-ruler initiate (Part B); full freedom, AI-vs-AI diplomacy unchanged.
- 2026-07-30 — Phase days retuned to Day 30 (Phase 1) / Day 44 (Phase 2) across all four sources, and `WarOfTheRingService.GetEffectivePhaseDays` now clamps `phase2 > phase1 >= 1` for every source. Previously only the JSON pair was validated (with a strictly-`<` check that let equal days through) while the MCM sliders — the live in-game path — were unvalidated, so an equal or inverted pair ran both transitions in one tick and the Isengard war was never observable.
- 2026-05-22 — War of the Ring phase defaults retuned to Day 2 (Phase 1) / Day 14 (Phase 2), both MCM-tunable.
- 2026-05-22 — Split peace + alliance invariants in `EnforcePermanentAlliances` (Mordor showing in both Wars and Alliances lists); closed Dale↔Isengard gap.
- 2026-05-22 — Promoted Harad (`empire_s ↔ aserai`) from Natural to Permanent alliance with Mordor + added MakePeace step in alliance enforcement.
- 2026-05-19 — Blocked war between same-alignment kingdoms (#203).
- 2026-05-13 — War of the Ring phase persistence via SyncData + config validation in the JSON providers (#129); plus prefix documentation + diagnostic logs (#152, #153).
- 2026-04-09 — Added `CanMakeAlliance` override to `TaomAllianceModel` as a hard gate (via `IDiplomacyService.IsAllianceAllowed`) for permanently hostile factions.
- 2026-03-27 — Added diagnostic/initialization logging to diplomacy enforcement hooks, behaviors, and the 3 diplomacy Harmony patches.

## GitHub Issue
- **Issue:** Unknown for the original feature (commits reference `16f7f4e` for initial implementation; no issue number in messages)
- **Kingdom vote deadlock guard (Patch80):** [#547](https://github.com/haterade22/TAOM/issues/547)
- **Seam D + the Player Switcher root fix:** [#550](https://github.com/haterade22/TAOM/issues/550), the second mechanism behind the same symptom. See [player-switcher.md](player-switcher.md) "Clan leadership follows the player".
- **War rule by phase:** #818 (open follow-ups #819, #820)
- **War of the Ring fixes:** #764 and #772 (closed with `triage-needs-ingame`; [war-of-the-ring.md](war-of-the-ring.md) "How to Test In-Game" steps 6 and 7)
- **Status:** Active

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/features/player-switcher.md](./player-switcher.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)
- [docs/modding/kingdoms.md](../modding/kingdoms.md)
- [docs/modding/recipe-add-a-kingdom.md](../modding/recipe-add-a-kingdom.md)

<!-- backlinks-end -->
