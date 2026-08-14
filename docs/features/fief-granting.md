# Fief Granting

## Overview

Changes who gets a town or castle after a kingdom captures it. Every kingdom holds an election for the
new owner, and this feature rewrites the scoring that decides it: the clan that actually stormed the
place gets a real claim, clans already sitting on many holdings get damped, clans of the settlement's
own culture are favoured, and a ruling clan that already holds most of the kingdom loses its right to
overrule the council. All weights are player-editable through MCM.

Also ships a one-time data pass that spread starting fief ownership in three kingdoms that opened
every campaign with a single clan holding every fortification.

## Why This Exists

- **Vanilla behavior:** On capture, `KingdomManager.SiegeCompleted` hands the settlement to
  `capturerParty.MapFaction.Leader`, the king. `SettlementClaimantCampaignBehavior` flags the town
  `IsOwnerUnassigned`, and the next daily settlement tick queues a `SettlementClaimantDecision`. Every
  eligible clan is scored by `CalculateMeritOfOutcome`, the top 3 go on the ballot, and every
  non-mercenary clan votes.
- **TAOM requirement:** Land should spread across a kingdom's houses, sit with culturally appropriate
  owners, keep weak clans able to field parties, and reward the clan that did the fighting.
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

- Support costs 20 / 60 / 100 influence for Slight / Strong / Full, and `DetermineSupportOption`
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

Vanilla's private `_capturerHero` is **not** carried across the swap. It is dead in v1.4.8 (written by
the constructor, never read) and all three producers pass `null` anyway, so the capturer signal comes
from `Town.LastCapturedBy` instead. It is still pulled into the save graph, so an engine version that
starts reading it would see `null` here. Worth re-checking at the next engine bump.

### Component Diagram

```
MCM (TaomSettings, group "Kingdom Politics/Fief Grants")
        |
  FiefGrantSettingsProvider (null-safe, NaN-safe clamps)
        |
  FiefGrantPolicyService (pure arithmetic over primitives)
        |
  TaomSettlementClaimantDecision  <-- swapped in by Patch70_FiefGrantDecisionSwap
   (sealed-type -> FiefGrantCandidateFacts conversion at the boundary)
        |
  vanilla SettlementClaimantDecision.CalculateMeritOfOutcome (kept, multiplied)
```

## Configuration

MCM is the **only** config surface for these weights. A parallel ModuleData copy would be a second
place to validate, and `.claude/rules/csharp-architecture.md` records CombatMechanics drifting exactly
that way when a JSON invariant and an MCM clamp were written by different hands.

Group: **Kingdom Politics/Fief Grants**. The weights are read live, so no restart and no new
campaign. One caveat on the master toggle, found in review: the swap happens when a decision is
CREATED, so turning the feature on will not retrofit an election that is already pending, and a
save carrying a pending vanilla decision from before the feature runs that one election on vanilla
scoring. Every election created afterwards picks up the current settings.

| Setting | Range | Default | Effect |
|---|---|---|---|
| Enable Fief Grant Rebalance | bool | on | Off restores exact vanilla scoring |
| Capturer Bonus | 1.0 to 5.0 | 2.50 | Multiplier for the clan that took the settlement |
| Landless Clan Bonus | 1.0 to 5.0 | 2.00 | Multiplier for a clan holding no fortification |
| Concentration Penalty | 0.0 to 1.0 | 0.35 | Damping as `1/(1 + fiefs * penalty)` |
| Culture Match Bonus | 1.0 to 3.0 | 1.50 | Multiplier when clan culture matches the settlement |
| Culture Mismatch Penalty | 0.1 to 1.0 | 0.60 | Multiplier when it does not |
| Ruling Clan Factor | 0.1 to 2.0 | 0.75 | Multiplier for the king's own clan |
| King's Vote Fief Share Cap | 0.0 to 1.0 | 0.34 | Share above which the king loses his override |
| Apply Penalties To Your Clan | bool | off | On scores your clan like any other |

Terms combine multiplicatively on top of vanilla's merit. Any non-finite or non-positive value falls
back to vanilla parity rather than inverting the ranking.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/FiefGranting/FiefGrantPolicyService.cs` | Scoring policy, pure over primitives |
| `Main/Features/FiefGranting/IFiefGrantPolicyService.cs` | Service interface |
| `Main/Features/FiefGranting/FiefGrantCandidateFacts.cs` | Primitive snapshot of one candidate clan |
| `Main/Features/FiefGranting/FiefGrantSettingsProvider.cs` | MCM reads with clamps |
| `Main/Features/FiefGranting/IFiefGrantSettingsProvider.cs` | Settings interface |
| `Main/Features/FiefGranting/TaomSettlementClaimantDecision.cs` | The subclass, boundary conversion |
| `Main/Features/FiefGranting/FiefGrantSaveableTypeDefiner.cs` | Save registration, base id 726900901 |
| `Main/Features/FiefGranting/Hooks/Patch70_FiefGrantDecisionSwap.cs` | Instance swap on `Kingdom.AddDecision` |
| `Main/Features/FiefGranting/FiefGrantingIoC.cs` | DryIoc registration |
| `tools/apply_starting_fief_spread.py` | Starting-ownership data pass and its drift check |

## Dependencies

- `IFiefGrantSettingsProvider` (this feature) reads `TaomSettings`
- `ICoopSessionProvider` (CoopInterop) supplies `ShouldDeferToHost`
- `IModLogger` (Core) for the one-shot fault report

## Save compatibility

`TaomSettlementClaimantDecision` needs a `SaveableTypeDefiner` because for the **player's** kingdom
`Kingdom.AddDecision` queues the decision into `_unresolvedDecisions`, which is a `[SaveableField]`.
AI kingdoms resolve their elections inline and never persist one, so this only matters for your own
kingdom. Base id `726900901`, localId `101`, giving global id `726901002`, clear of LotrIssue's
`726900902`. `SaveDefinerCollisionGuard` reports any clash with another mod at startup.

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
3. **Villages are untouched.** None of the 607 villages carries an explicit `owner`; each follows its
   bound fortification.
4. **Lindon and Goblins are still at 100%** because each holds exactly one fortification. That is
   arithmetic, not concentration, and redistribution cannot fix it.

Kingdoms where clans outnumber fiefs (Rohan 22 clans / 14 fiefs, Dol Guldur 15 / 6, Misty Mountain
Orcs 15 / 10, Isengard 11 / 4) will always have landless clans. Adding fiefs or merging clans there is
a separate design question, deliberately out of scope.

## Tests

- `TAOM.Tests/Features/FiefGranting/FiefGrantPolicyServiceTests.cs`, 20 tests: disabled parity,
  concentration damping, landless and capturer bonuses, culture terms, multiplicative combination, the
  player exemption, non-finite settings, and the King's Vote share gate including its boundary.
- `TAOM.Tests/Features/FiefGranting/Patch70FiefGrantDecisionSwapBindingTests.cs`, 4 binding tests
  against the installed engine: `Kingdom.AddDecision`'s parameter name (Harmony binds by name), the
  class still being subclassable with its 4-argument constructor, both scoring members still virtual
  and not final, and the carried-over state still readable and writable.

## How to retune the weights

1. In game, Options, Mod Options, TAOM, Kingdom Politics/Fief Grants.
2. Change a slider. It takes effect on the next election to be CREATED, with no restart and no
   reload. An election already pending in your kingdom keeps the scoring it was created with.
3. To go back to stock behaviour, turn off Enable Fief Grant Rebalance. That restores vanilla exactly,
   including the unlimited King's Vote.

No code changes needed. To change the ranges themselves, both the MCM attribute in
`Main/Features/TaomSettings.cs` and the clamp in `FiefGrantSettingsProvider.cs` must move together.

## Co-op

The swap is skipped entirely when `ICoopSessionProvider.ShouldDeferToHost` is true, so a client runs
vanilla and takes the host's result. This matters more than usual here because `GetAiChoice` calls
`MBRandom` for the King's Vote roll, so two peers scoring differently would diverge.

The nine MCM settings are classified simulation-relevant by `CoopSettingsRelevance` (include by
default), which is correct: two peers with different fief weights allocate land differently. They are
covered by `SettingsFingerprint`.

## Changelog

- 2026-08-14: feature added (#458). Patch70 decision swap, MCM weights, King's Vote share cap, and
  the starting-ownership data pass for Lasgalen, Imladris and Lothlorien.

## GitHub Issue

- **Issue:** #458, [Fief grants concentrate in one clan per kingdom](https://github.com/haterade22/TAOM/issues/458)
- **Status:** Closed 2026-08-14 on the build, test and review evidence. **The in-game smoke tests
  were never run**, so the code is verified correct but the campaign is not verified to play right.
  Reopen if a smoke test contradicts anything here.
- **Follow-up:** [#460](https://github.com/haterade22/TAOM/issues/460) carries the two design
  questions the Codex pass surfaced (merit weights the election but does not decide it; the King's
  Vote cap binds AI rulers only).
