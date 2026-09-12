# Fief Granting

## Overview

Changes who gets a town or castle after a kingdom captures it. Every kingdom holds an election for the
new owner, and this feature rewrites the scoring that decides it: the clans that actually fought in
the assault get a claim in proportion to how much they fought, clans that were not there are damped,
clans already sitting on many holdings are damped, clans of the settlement's own culture are
favoured, and a ruling clan that already holds most of the kingdom loses its right to overrule the
council. All weights are player-editable through MCM.

Also ships a one-time data pass that spread starting fief ownership in three kingdoms that opened
every campaign with a single clan holding every fortification.

## Why This Exists

- **Vanilla behavior:** On capture, `KingdomManager.SiegeCompleted` hands the settlement to
  `capturerParty.MapFaction.Leader`, the king. `SettlementClaimantCampaignBehavior` flags the town
  `IsOwnerUnassigned`, and the next daily settlement tick queues a `SettlementClaimantDecision`. Every
  eligible clan is scored by `CalculateMeritOfOutcome`, the top 3 go on the ballot, and every
  non-mercenary clan votes.
- **TAOM requirement:** Land should spread across a kingdom's houses, sit with culturally appropriate
  owners, keep weak clans able to field parties, and reward the clans that did the fighting.
- **Without this feature:** Holdings pile up in one clan per kingdom.

### Why the feature targets merit rather than the ballot

In `DetermineSupport`, a clan evaluating itself adds:

```csharp
initialMerit += 0.2f * Settlement.GetSettlementValueForFaction(clan)
                     * Campaign.Current.Models.DiplomacyModel.DenarsToInfluence();
```

`DenarsToInfluence()` returns `0.002f` (`DefaultDiplomacyModel.cs:970`), and a town is worth
`750000 + Prosperity * 1000` plus its bound villages before the `SettlementValueModel` multipliers. So
that self term lands near 700 against an `InitialMerit` in the low tens. After the trait multipliers
and the final `*2` self-multiplier, a clan backs itself at roughly 40x what it gives any rival.

A finalist that can afford it therefore reaches `FullyPush`, worth 3 points. When they all can, they
tie, and `TaleWorlds.Core.Extensions.MaxBy` uses a strictly-greater comparison so it keeps element 0
of a list `NarrowDownCandidates` already sorted by merit descending.

**Merit therefore dominates the outcome, but it does not decide it outright.** An earlier version of
this doc claimed it did; an adversarial review pass refuted that, and the correction matters:

- Support costs 20 / 60 / 100 influence for Slight / Strong / Full (this decision type overrides the
  base 20 / 60 / 150, `SettlementClaimantDecision.cs:290-306`), and `DetermineSupportOption`
  **downgrades a vote the clan cannot afford**. A top-merit finalist sitting on 59 influence casts one
  point while two poorer-merit finalists with 100+ cast three each, and loses 1 to 3 to 3.
- Every non-mercenary clan votes, not just the three finalists, so a wealthy fourth clan can push
  three points onto a lower-merit finalist it likes.

So the tie is common, not guaranteed, and TAOM's merit weighting biases the election strongly without
determining it. That is an acceptable outcome (elections that are always predictable would be worse
play), but the honest framing is "heavily weighted", not "decided".

### The two concentration drivers

Vanilla's merit already opposes hoarding: it divides by the value of fortifications the clan holds and
adds `+30` for holding none. Worked through, a landless tier-2 clan out-scores a six-fief tier-6 ruler.
The scoring was not the problem. These were:

1. **The King's Vote.** `IsKingsVoteAllowed` is `true` for fief grants (only
   `KingSelectionKingdomDecision` overrides it to false). In `KingdomElection.GetAiChoice` the king's
   preferred outcome is himself for the same self-vote reason, so his preference gap is enormous,
   capped only by `_chooser.Influence`. The override fires once that exceeds `300 + overrideCost`.
   TAOM seeds clans 400 to 600 influence at campaign start, clearing the threshold on day one.
2. **Starting ownership.** Lasgalen opened with `clan_mirkwood_1` holding 7 of 7, Imladris with
   `clan_rivendell_1` holding 5 of 5, Lothlorien with `clan_lothlorien_1` holding 4 of 4. Authored
   state, not an election outcome.

### Why players got fiefs they never fought for (#565)

The first version shipped on 2026-08-14 without an in-game smoke test, and the reports that followed
were that test arriving late: players were winning towns and castles after sieges they took no part
in. Nothing in the feature had changed since it landed. Four things in its shipped shape stacked.

Vanilla merit, `SettlementClaimantDecision.CalculateMeritOfOutcome` (v1.4.8, lines 170-229):

```
(Tier*30 + Strength/10 + 30[landless] + 30[Town.LastCapturedBy == clan] + 60[ruler] + poor(0..30) + 30[player])
   / (value of the clan's other fiefs + this fief's value) * proximity * 200000
```

1. **The only participation signal was one sticky clan stamp.** `Town.LastCapturedBy` is written in
   exactly one place, `ChangeOwnerOfSettlementAction.ApplyBySiege`, to `capturerParty.Party.Owner.Clan`,
   where `capturerParty` is `MapEvent.AttackerSide.LeaderParty`: the army leader. An army member got
   nothing. It is never cleared, so a clan that stormed the place months ago kept a 2.5x claim on
   every later re-election of that settlement (a lord leaving the faction or a clan being destroyed
   both reopen the claim through `openToClaim`).
2. **The player exemption.** "Apply Penalties To Your Clan" shipped OFF, so a landed player skipped
   the concentration and mismatch terms every AI clan paid. At two fiefs and a culture mismatch that
   is `1/1.7 * 0.6 = 0.35` for the AI clan against `1.0` for the player, a 2.9x swing on top of
   vanilla's own flat `+30` for the player (`num12`, line 226).
3. **Landless dominance is mostly vanilla's, and TAOM doubled it.** A landless clan has nothing in the
   divisor and gets the maximal proximity factor (`num5 = 1`, clamped to the `0.25` floor, so
   `num6 = 4^(avgTownDistance*0.0076)`, about 2.9 on this map). Worked: a landless tier-2 player
   (numerator ~160) scores roughly `160/1M * 2.9 = 464` before TAOM and `1392` after the `x2 x1.5`;
   a tier-4 clan holding two fiefs (numerator ~170, divisor ~3M, proximity ~1.5) scores `85` before
   and `50` after. Nothing about participation entered that comparison.
4. **The King's Vote cap exposed it.** In vanilla the king's override usually took the fief for the
   crown, hiding the merit ranking. TAOM's cap removed that, so the merit winner, the player above,
   actually won.

The fix is the participation record below plus the renamed exemption knob. The landless bonus stays
at 2.0 and is documented in its hint as the vanilla amplifier it is.

## Architecture

### Design Challenge

The scoring lives on `SettlementClaimantDecision`, which the engine constructs itself in three places.
There is no GameModel seam for it: `DefaultKingdomDecisionPermissionModel.IsAnnexationDecisionAllowed`
only gates whether the election runs at all, and `SettlementValueModel` moves the numbers but is also
read by diplomacy, AI targeting, and clan expulsion, so retuning it there has a blast radius well
beyond fief grants.

### Solution Approach

`SettlementClaimantDecision` is public and not sealed, and both scoring members are virtual, so TAOM
subclasses it and swaps the instance in. No transpiler, no reflection into engine internals.

`Kingdom.AddDecision` is the single chokepoint. All three producers funnel through it:

| Producer | Path |
|---|---|
| `SettlementClaimantCampaignBehavior.DailyTickSettlement` | war capture, `capturerHero` passed as `null` |
| `SettlementClaimantPreliminaryDecision.ApplyChosenOutcome` | annexation follow-up, sets `IsEnforced = true` |
| `KingdomManager.RelinquishSettlementOwnership` | a lord giving a fief up, passing the owner clan as both proposer and `clanToExclude` |

The third was missed when this was first written and found by a review pass; the doc said "both
producers" for a day. It is worth stating plainly because it is the argument for the design: patching
the sink rather than the producers caught a path nobody knew about. Patching
`DailyTickSettlement` directly, which was the alternative considered, would have left it on vanilla
scoring silently. It is also why `ClanToExclude` must survive the swap: that producer uses it to stop
the relinquishing clan winning its own fief straight back.

The second path sets `IsEnforced` on the line **before** it calls `AddDecision`, so the flag is
present on the instance Patch70 replaces and is copied onto the replacement. Verified against the
v1.4.8 decompile. If that ordering ever inverts, an enforced annexation silently stops being enforced,
and no test can see it: `Patch70FiefGrantDecisionSwapBindingTests` pins the members the copy needs, but
the ordering itself is only provable by re-reading the engine after a bump.

The merit override **multiplies** vanilla rather than replacing it, so the proximity factor and the
settlement-value divisor stay exactly as TaleWorlds wrote them.

### The participation record (#565)

Vanilla already computes what the election needs. When a siege battle ends,
`SiegeAftermathCampaignBehavior.OnMapEventEnded` reads every winning-side party's
`MapEventParty.ContributionToBattle` and splits the loot by it. `FiefGrantingCampaignBehavior`
records the same numbers per clan into `IFiefSiegeParticipationService`, and the decision reads
them back through `FiefGrantFactsBuilder`.

**Ordering.** `MapEvent.FinalizeEventAux` dispatches `MapEventEnded` (v1.4.8 `MapEvent.cs:2079`)
before `SiegeCompleted` (`:2093`), which is where `KingdomManager.SiegeCompleted` calls
`ChangeOwnerOfSettlementAction.ApplyBySiege`. So the record is written first, inside the same
finalize call, and the `BySiege` owner change that follows keeps it when a claim will open (below).
Campaign-event listener dispatch is LIFO, so TAOM's handler runs before vanilla's on the same event;
it only reads. The rules the recorder applies each mirror one engine site and live in
`FiefSiegeCaptureRules`, one method per site, so each can be tested on bare engine objects.

**Which battles, which parties.** The recorder mirrors the battle-type gate that actually transfers
ownership, `KingdomManager.SiegeCompleted` (`KingdomManager.cs:238-240`): an attacker victory in an
assault (`IsSiegeAssault`), or a defender victory in a sally-out (`IsSallyOut`, `IsBlockadeSallyOut`,
where the garrison fights as attacker and the besiegers are the defender side). It deliberately does
NOT mirror vanilla's loot handler, which also counts `SiegeOutside`: a relief battle outside the walls
captures nothing, and the first cut of this recorder wrote a phantom record for it that nothing ever
cleared (found in review). Each `MapEventParty` maps to `MobileParty.ActualClan`, falling back to
`MobileParty.Owner.Clan` (never `PartyBase.Owner`, a throwing computed getter that
`PartyOwnerGetterBanTests` forbids assembly-wide). A clan fielding several parties is summed.

**Which clans.** Only clans that can appear on the ballot: `DetermineInitialCandidates` enumerates
the capturing kingdom's clans and drops those under mercenary service, so the recorder keeps a clan
only if its `MapFaction` is the winning side's leader-party faction (the one
`KingdomManager.SiegeCompleted` hands the settlement to) and it is not a mercenary. Both matter
because every share is measured against the top recorded clan: `SiegeEvent.CanPartyJoinSide` lets a
party of any faction at war with the defenders and at peace with the besiegers join the assault, and
an allied outsider or a mercenary company that out-fought the field would otherwise cap every real
candidate below 1.0 (deep review and Codex, 2026-09-11).

**Keep and forget.** `OnSettlementOwnerChanged` keeps the record on `BySiege` only when vanilla will
open a claim for that capture, and forgets it on every other detail:

| Detail | Effect on the record |
|---|---|
| `BySiege`, fortification taken by a kingdom with more than one clan | kept (written moments earlier in the same finalize); the grant that follows clears it |
| `BySiege`, any other case (a sole-clan kingdom, a clan faction, a village) | forgotten: vanilla never sets `IsOwnerUnassigned` (`SettlementClaimantCampaignBehavior` lines 39-42), so no grant would ever clear it and a relinquish or annexation election years later would read it (Codex F2) |
| `ByKingDecision` (the grant itself, via `ApplyChosenOutcome`) | forgotten |
| `ByLeaveFaction`, `ByClanDestruction` | forgotten, so the re-election they open runs with no record |
| `ByBarter`, `ByGift`, `ByRebellion`, `Default` | forgotten |

Two of the three producers (the daily tick after a capture, the annexation follow-up) only run after
one of those owner changes. The third, `KingdomManager.RelinquishSettlementOwnership`, creates an
election for a settlement its owner still holds, with no owner change before it, so anything on
record for that settlement at that moment is read. That is why the recorder only writes on the
battle types that capture: after a capture and its grant the record is already gone
(`ByKingDecision` clears it), and no non-capturing battle writes one. A save between the capture and
the election carries the record, which matters for the player's own kingdom, where the decision waits
up to 48 hours in `_unresolvedDecisions`. If an enemy re-takes the settlement in that window, vanilla's
`SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged` removes the pending decision from every
kingdom and the new assault overwrites the record.

**Scoring.** `FiefGrantCandidateFacts.SiegeContributionShare` is the clan's contribution over the top
clan's, in `[0, 1]`, with `SiegeWasRecorded` beside it. The clan that carried the assault gets the
full Siege Participation Bonus, every other participant gets `1 + (bonus - 1) * share`, and a clan
with no party in the assault gets the Absent From Siege Factor. With no record every clan stays at
1.0, so the ranking is unchanged rather than uniformly rescaled. The absent factor is about what a
clan did, not what it holds, so the player exemption does not skip it.

One consequence worth knowing (Codex, 2026-09-11): the absent factor changes merit magnitudes, and
`KingdomDecision.DetermineSupportOption` compares a supporter's willingness (derived from the
candidates' merit) against the 20 / 60 / 100 influence thresholds. Halving the finalists' merit can
move a non-finalist supporter from Strongly Favor to Slightly Favor even when the ranking is
unchanged. That is how vanilla consumes merit, not a scoring bug, and it is the intended weight of
the knob; set it to 1.00 to switch it off.

**Restoring a hand-edited save.** The writer emits each clan once, so a repeated clan id inside a
restored string is treated as malformed and skipped rather than summed; summing two longs unchecked
could wrap into a negative share (Codex F3).

**Why not the engine's own fields.** Vanilla's private `_capturerHero` is dead in v1.4.8 (written by
the constructor, never read) and all three producers pass `null`; `Town.LastCapturedBy` names one
clan and is never cleared. Both carry one identity where a share needs every clan. Vanilla's own
`+30` on `LastCapturedBy` is left exactly as it is. `OnSiegeAftermathAppliedEvent` was also
rejected: for a player-led siege it fires only after the aftermath menu choice, after ownership has
already changed. `_capturerHero` is still pulled into the save graph, so an engine version that
starts reading it would see `null` here; worth re-checking at the next engine bump.

### What this deliberately does NOT do

`DetermineSupport` is left alone, and this is now a known limitation rather than a clean argument.
The original reasoning was that the ballot always ties so the override would change nothing; that was
wrong (see above). Overriding it so every AI supporter backs the merit winner would make the feature
deterministic, at the cost of removing kingdom politics from the result entirely. Deferred as a
design question rather than decided quietly: tracked in [#460](https://github.com/haterade22/TAOM/issues/460).

**The King's Vote cap is AI-only.** `IsKingsVoteAllowed` is read solely by
`KingdomElection.GetAiChoice`. When the PLAYER rules the kingdom, `OnPlayerSupport` assigns the chosen
outcome directly without consulting the property, so a player ruler holding most of the map can still
grant themselves every fief. That is arguably correct (it is the player's kingdom), but the setting's
name does not say so, and the cap does not restrain you the way it restrains an AI king. Also
tracked in [#460](https://github.com/haterade22/TAOM/issues/460).

### Component Diagram

```
MCM (TaomSettings, group "Kingdom Politics/Fief Grants")
        |
  FiefGrantSettingsProvider (null-safe, NaN-safe clamps)
        |
  FiefGrantPolicyService (pure arithmetic over primitives)
        |
  TaomSettlementClaimantDecision  <-- swapped in by Patch70_FiefGrantDecisionSwap
   (sealed-type -> FiefGrantCandidateFacts conversion in FiefGrantFactsBuilder)
        |                              ^
        |                              | share per clan (#565)
        |                    IFiefSiegeParticipationService  <-- FiefGrantingCampaignBehavior
        |                     (per-settlement record, SyncData)    (MapEventEnded, OnSettlementOwnerChanged)
        |                                                                  |
        |                                                        FiefSiegeCaptureRules
        |                                                 (which battles capture, which clans may
        |                                                  claim, when vanilla opens a claim)
        |
  vanilla SettlementClaimantDecision.CalculateMeritOfOutcome (kept, multiplied)
```

## Configuration

MCM is the **only** config surface for these weights. A parallel ModuleData copy would be a second
place to validate, and `.claude/rules/csharp-architecture.md` records CombatMechanics drifting exactly
that way when a JSON invariant and an MCM clamp were written by different hands.

Group: **Kingdom Politics/Fief Grants**. The weights are read live on every merit calculation, so
no restart and no new campaign, and a TAOM decision already pending in your kingdom scores with
whatever the sliders say when it is next evaluated (the second Codex pass corrected an earlier
claim that a pending election "keeps the scoring it was created with"). The one creation-time
decision is the master toggle: the swap happens when a decision is CREATED, so turning the feature
on will not retrofit an election that is already pending, and a save carrying a pending vanilla
decision from before the feature runs that one election on vanilla scoring.

| Setting | Property | Range | Default | Effect |
|---|---|---|---|---|
| Enable Fief Grant Rebalance | `EnableFiefGrantRebalance` | bool | on | Off restores exact vanilla scoring |
| Siege Participation Bonus | `FiefGrantCapturerBonus` | 1.0 to 5.0 | 2.50 | Full multiplier for the clan that carried the assault; `1 + (bonus - 1) * share` for every other participant |
| Absent From Siege Factor | `FiefGrantAbsentFromSiegeFactor` | 0.1 to 1.0 | 0.50 | Multiplier for a clan with no party in the assault; only when a record exists; applies to your clan regardless of the exemption |
| Landless Clan Bonus | `FiefGrantLandlessBonus` | 1.0 to 5.0 | 2.00 | Multiplier for a clan holding no fortification |
| Concentration Penalty | `FiefGrantConcentrationPenalty` | 0.0 to 1.0 | 0.35 | Damping as `1/(1 + fiefs * penalty)` |
| Culture Match Bonus | `FiefGrantCultureMatchBonus` | 1.0 to 3.0 | 1.50 | Multiplier when clan culture matches the settlement |
| Culture Mismatch Penalty | `FiefGrantCultureMismatchPenalty` | 0.1 to 1.0 | 0.60 | Multiplier when it does not |
| Ruling Clan Factor | `FiefGrantRulingClanFactor` | 0.1 to 2.0 | 0.75 | Multiplier for the king's own clan |
| King's Vote Fief Share Cap | `FiefGrantKingsVoteFiefShareCap` | 0.0 to 1.0 | 0.34 | Share above which the king loses his override |
| Exempt Your Clan From Penalties | `FiefGrantExemptPlayerClanFromPenalties` | bool | off | On keeps every bonus but never damps your clan for its holdings or a culture mismatch |

Terms combine multiplicatively on top of vanilla's merit. Any non-finite or non-positive value falls
back to vanilla parity rather than inverting the ranking.

**The exemption knob was renamed, not flipped.** It shipped as "Apply Penalties To Your Clan" with
the exemption ON (`FiefGrantApplyPenaltiesToPlayerClan = false`). MCM keeps a saved value per
property, so flipping that compiled default would have reached fresh installs only; the property was
renamed to `FiefGrantExemptPlayerClanFromPenalties`, default off, so every install picks the new
default up and the orphaned key in `TAOM.json` is ignored. The provider inverts it back into the
policy's `ApplyPenaltiesToPlayerClan`, so the policy and its tests did not churn.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/FiefGranting/FiefGrantPolicyService.cs` | Scoring policy, pure over primitives |
| `Main/Features/FiefGranting/IFiefGrantPolicyService.cs` | Service interface |
| `Main/Features/FiefGranting/FiefGrantCandidateFacts.cs` | Primitive snapshot of one candidate clan |
| `Main/Features/FiefGranting/FiefGrantFactsBuilder.cs` | Boundary conversion: counts holdings, reads the participation share |
| `Main/Features/FiefGranting/FiefSiegeParticipationService.cs` | Per-settlement record of who fought and how much (#565) |
| `Main/Features/FiefGranting/FiefSiegeCaptureRules.cs` | The engine-derived rules: which battles capture, which clans may claim, when vanilla opens a claim (#565) |
| `Main/Features/FiefGranting/IFiefSiegeParticipationService.cs` | Record interface |
| `Main/Features/FiefGranting/Hooks/FiefGrantingCampaignBehavior.cs` | Writes the record at `MapEventEnded`, keeps it only for a claim-opening capture and clears it on any other transfer, owns SyncData (#565) |
| `Main/Features/FiefGranting/FiefGrantSettingsProvider.cs` | MCM reads with clamps |
| `Main/Features/FiefGranting/IFiefGrantSettingsProvider.cs` | Settings interface |
| `Main/Features/FiefGranting/TaomSettlementClaimantDecision.cs` | The subclass, boundary entry point |
| `Main/Features/FiefGranting/FiefGrantSaveableTypeDefiner.cs` | Save registration, base id 726900901 |
| `Main/Features/FiefGranting/Hooks/Patch70_FiefGrantDecisionSwap.cs` | Instance swap on `Kingdom.AddDecision` |
| `Main/Features/FiefGranting/FiefGrantingIoC.cs` | DryIoc registration |
| `tools/apply_starting_fief_spread.py` | Starting-ownership data pass and its drift check |

## Dependencies

- `IFiefGrantSettingsProvider` (this feature) reads `TaomSettings`
- `ICoopSessionProvider` (CoopInterop) supplies `ShouldDeferToHost` for the swap and `IsAuthority`
  for the recorder
- `IModLogger` (Core) for the one-shot fault report, the per-capture record line and the malformed
  save-record warning

## Save compatibility

`TaomSettlementClaimantDecision` needs a `SaveableTypeDefiner` because for the **player's** kingdom
`Kingdom.AddDecision` queues the decision into `_unresolvedDecisions`, which is a `[SaveableField]`.
AI kingdoms resolve their elections inline and never persist one, so this only matters for your own
kingdom. Base id `726900901`, localId `101`, giving global id `726901002`, clear of LotrIssue's
`726900902`. `SaveDefinerCollisionGuard` reports any clash with another mod at startup.

The participation record rides `FiefGrantingCampaignBehavior.SyncData` under the key
`_taomFiefSiegeParticipation` as a `Dictionary<string, string>` (settlement id to
`clanId=contribution;clanId=contribution`, clans sorted by id), the `SiegeDefenseService` shape, so
no new saveable class. A save written before #565 has no key and loads as an empty record; a fresh
campaign never runs the loading half, so the behavior resets the singleton on session launch (the
Refuge/FieldCamp latch). Malformed parts of a saved string are skipped with one warning and the
affected clans score as absent.

## The starting-ownership data pass

`tools/apply_starting_fief_spread.py` reassigns 10 fortifications across three kingdoms. Run with no
arguments to check, `--apply` to write (a `.bak` is kept).

| Kingdom | Before | After |
|---|---|---|
| Lasgalen | `clan_mirkwood_1` holds 7 of 7 | ruler keeps Felegoth and Glad Thaw, five houses take one each |
| Imladris | `clan_rivendell_1` holds 5 of 5 | ruler keeps Rivendell and Hithaegrist, 2 to `_2`, 1 to `_3` |
| Lothlorien | `clan_lothlorien_1` holds 4 of 4 | ruler keeps Caras Galadhon and Cerin Amroth, 1 each to `_2` and `_3` |

Four things to know:

1. **The target file is `Modules/TAOM_Map/ModuleData/settlements.xml`, which this repo does not
   track.** A module reinstall silently reverts the edit. Re-run the check after any TAOM_Map update.
   The repo's `Main/_Module/ModuleData/settlements.xml` is the stale shadow (863 settlements against
   the live file's 988) and editing it does nothing.
2. **New campaigns only.** Settlement ownership is engine-saved, unlike `Settlement.Culture`.
3. **Villages are untouched.** None of the 616 villages carries an explicit `owner`; each follows its
   bound fortification.
4. **Lindon and Goblins are still at 100%** because each holds exactly one fortification. That is
   arithmetic, not concentration, and redistribution cannot fix it.

Kingdoms where clans outnumber fiefs (Rohan 22 clans / 14 fiefs, Dol Guldur 15 / 6, Misty Mountain
Orcs 15 / 10, Isengard 11 / 4) will always have landless clans. Adding fiefs or merging clans there is
a separate design question, deliberately out of scope.

## Tests

- `TAOM.Tests/Features/FiefGranting/FiefGrantPolicyServiceTests.cs`: disabled parity, concentration
  damping, landless bonus, the participation term (full share, scaled share, share above one clamped,
  absent with a record, no record leaves both terms out, non-finite share, the exemption not skipping
  the absent factor), culture terms, multiplicative combination, the player exemption, non-finite
  settings, and the King's Vote share gate including its boundary.
- `TAOM.Tests/Features/FiefGranting/FiefSiegeParticipationServiceTests.cs`: per-clan summing, share
  relative to the top clan, zero and negative contributions, empty ids, replace on re-record,
  separator-carrying ids refused, forget, snapshot encoding and round trip, malformed save parts,
  session reset.
- `TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorSessionResetTests.cs`: the session-reset
  latch, both SyncData directions, and the forget and authority gates of both handlers
  (reflection-invoked with uninitialized engine objects), including a siege that opens no claim
  forgetting its own record.
- `TAOM.Tests/Features/FiefGranting/FiefGrantingBehaviorCaptureGateTests.cs`: every rule in
  `FiefSiegeCaptureRules` on bare engine objects: one row per `MapEvent.BattleTypes` value for which
  side captures (the relief-battle row is the gap 1 regression), the mercenary and foreign-faction
  exclusions (gap 2 and Codex F1), when vanilla opens a claim on a staged one-clan and two-clan
  kingdom (Codex F2), and the clan lookup on a settlement party.
- `TAOM.Tests/Features/FiefGranting/Patch70FiefGrantDecisionSwapBindingTests.cs`, 4 binding tests
  against the installed engine: `Kingdom.AddDecision`'s parameter name (Harmony binds by name), the
  class still being subclassable with its 4-argument constructor, both scoring members still virtual
  and not final, and the carried-over state still readable and writable.
- `TAOM.Tests/Features/FiefGranting/FiefSiegeParticipationBindingTests.cs`, 4 binding tests: the
  `MapEvent` side and outcome members, `MapEventParty.ContributionToBattle` as an int, the clan
  lookups (`PartyBase.MobileParty`, `MobileParty.ActualClan`, `MobileParty.Owner`,
  `Clan.IsUnderMercenaryService`), and the two `CampaignEvents` the recorder listens to including
  the six-argument owner-changed signature and the `BySiege` detail.

## How to retune the weights

1. In game, Options, Mod Options, TAOM, Kingdom Politics/Fief Grants.
2. Change a slider. The weights are read live, so it takes effect on the next merit calculation,
   including a TAOM election already pending in your kingdom. Only the master toggle is
   creation-time (see Configuration).
3. To go back to stock behaviour, turn off Enable Fief Grant Rebalance. That restores vanilla exactly,
   including the unlimited King's Vote.

No code changes needed. To change the ranges themselves, both the MCM attribute in
`Main/Features/TaomSettings.cs` and the clamp in `FiefGrantSettingsProvider.cs` must move together.
To change a compiled DEFAULT for everyone, rename the property (see Configuration): a flipped
default on an existing name reaches fresh installs only.

## Co-op

The swap is skipped entirely when `ICoopSessionProvider.ShouldDeferToHost` is true, so a client runs
vanilla and takes the host's result. This matters more than usual here because `GetAiChoice` calls
`MBRandom` for the King's Vote roll, so two peers scoring differently would diverge. The recorder
gates both of its handlers on `IsAuthority`: a client keeps no record of its own and takes the host's
through the save.

Inherited limitation, on file since #458 and restated by the second Codex pass: a
`TaomSettlementClaimantDecision` restored from the host's save never passes through
`Kingdom.AddDecision`, so Patch70's `ShouldDeferToHost` skip does not apply to it and the decision
scores with TAOM's policy on the client too. Whether that matters depends on the co-op layer
overwriting the client's result, which has not been verified on two machines. Tracked with the
other co-op verification items in [coop-interop.md](coop-interop.md).

The ten MCM settings are classified simulation-relevant by `CoopSettingsRelevance` (include by
default), which is correct: two peers with different fief weights allocate land differently. They are
covered by `SettingsFingerprint`.

## Changelog

- 2026-09-11: #565. Participation replaces the `Town.LastCapturedBy` stamp: a per-clan record of the
  winning assault, share-scaled bonus, a new Absent From Siege Factor, and the player exemption
  renamed so its new default (scored like any clan) reaches existing installs.
- 2026-08-14: feature added (#458). Patch70 decision swap, MCM weights, King's Vote share cap, and
  the starting-ownership data pass for Lasgalen, Imladris and Lothlorien.

## GitHub Issue

- **Issue:** #458, [Fief grants concentrate in one clan per kingdom](https://github.com/haterade22/TAOM/issues/458).
  Closed 2026-08-14 on build, test and review evidence with no in-game smoke test; the player reports
  behind #565 were that smoke test, and it failed.
- **Issue:** #565, [Fief grants: players win fiefs from sieges they never joined](https://github.com/haterade22/TAOM/issues/565).
  Open until the smoke tests below pass.
- **Follow-up:** [#460](https://github.com/haterade22/TAOM/issues/460) carries the two design
  questions the first review surfaced (merit weights the election but does not decide it; the King's
  Vote cap binds AI rulers only). Unchanged by #565.

## Smoke tests owed (#565)

None of these has been run. The code is verified by the suites above and by review; the campaign is
not yet verified to play right.

1. Join an AI army of your kingdom as a member, take a castle, wait for the election. The log shows
   one `[FiefGrant] <settlement> stormed: N winning parties on record` line at battle end, and the
   election favours your clan by its share.
2. Stay away from a capture by your kingdom. Your clan is damped (the absent factor shows in the
   election's merit order) and a clan that fought should win over it. The ballot and a player
   ruler's own choice can still hand an absent clan the fief; that alone is not a defect.
3. Save between the capture and the election, reload, let the election run: the record survives.
4. On an install that played the first version, Mod Options shows Exempt Your Clan From Penalties
   OFF without a reset.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
