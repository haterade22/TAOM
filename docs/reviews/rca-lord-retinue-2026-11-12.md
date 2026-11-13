# RCA: Lord Retinue (#1122), 2026-11-12

## Top-line

The first `/deep-review` of LordRetinue (tier 1: a campaign tick, hero state, co-op authority) ran two lenses at max effort, Engine compatibility and Data flow, and found 3 HIGH, 1 MEDIUM and 6 LOW defects in a change whose 12 unit tests were green. Both lenses reached the same three HIGH findings independently. A second pass on the fix diff (same two lenses, `effort: "high"`) found no HIGH or MEDIUM and four more LOW items. All findings were verified in the v1.5.7 source before a fix. The Codex pass was skipped (out of usage, 2026-11-12).

The defects share one cause. The change put state into containers vanilla never fills for AI parties (a hero in an AI lord party's roster, a party role on an AI party), and every vanilla reader and cleanup of that state assumes it is empty. The tests covered only the pure decision service.

## Findings

| # | Sev | Defect | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | A party whose leader marries away disbands with a retinue hero aboard. `MarriageAction` removes only the leader and calls `StartDisband`; the garrison merge copies the whole roster and then clears it, which leaves the hero in the garrison roster with `PartyBelongedTo` null | Vanilla consumer of new state | The handover said vanilla "mostly handles this", and my plan and doc said "left to vanilla". I did not read `DisbandPartyCampaignBehavior` or `MarriageAction`. | Lesson 1. Release the heroes when a disband starts. |
| 2 | HIGH | The role pick started at skill 0, so a hero with Steward 30 replaced a leader with Steward 120. `Effective*` falls back to the leader, so the leader already fills every role, and the Steward effect then cut the party size limit by about 22 | Wrong model of an empty slot | I modeled an empty role as "no holder". My own first message quoted the leader fallback. The test fixtures all started from a zero leader. | Lesson 2. Compare with the leader's skill; rank by gain. |
| 3 | HIGH (co-op) | Player checks used `Clan.PlayerClan`, `MobileParty.MainParty` and `IsHumanPlayerCharacter`, which name only the local peer; a remote player's party and kin looked like AI | Repeat of #966 and the 2026-10-06 lesson | I read the sibling `AlignmentDesertionBehavior`, which uses `IPlayerControlQuery`, and copied its tick but not its gate. Nothing scans a new behavior for local-peer statics. | Repeat entry on the existing lesson. Gate owed (below). |
| 4 | MEDIUM | A hero aboard is never a vanilla leader candidate (`PartyBelongedTo != null` in both commander searches), and the take rule protected only the day it ran, so a clan that gained a party slot found no reserve commander | Vanilla consumer of new state | The plan said "leader-material heroes are left alone" and I took that as covering later days. | Take only at the party limit; release the retinue below it. |
| 5 | LOW | `CouldLeadParty` never filtered: `GetBestAvailableCommander` has a second loop for AI clans that drops `CanLeadParty()` | Unread vanilla branch | I read the first loop only. | Rule reduced to "no take while below the limit". |
| 6 | LOW | The doc said "in no map event or siege" but the code checked only `BesiegerCamp` | Claim without a quoted line | I wrote the doc from the plan. | Gate adds `Settlement.IsUnderSiege`. |
| 7 | LOW | Hints and doc overstated: "When off, vanilla", "left alone", and a return time for released heroes | Claim without a quoted line | Same. | Reworded; a hero released at a disband away from a settlement returns in about 3 days. |
| 8 | LOW | Vanilla's role gate (`CanBeGovernorOrHavePartyRole`) and move gate (`CanMoveToSettlement`) were skipped | Unread vanilla precondition | I listed guards from my own idea of "idle". | Both are now eligibility facts. |
| 9 | LOW | The cap bound of 2 existed twice (the attribute and the clamp) | Duplicated constant | Not checked. | One public constant feeds both. |
| 10 | LOW | Wage, party-size slot and food costs of an extra hero were undocumented | Missing doc | Plan step 3 ("does an extra hero count against caps") was never run. | Costs section in the feature doc. |
| 11 | LOW | The 14 skip guards of the candidate filter had no test (tests.md "Skip-Guard Exhaustion"); they sat in the behavior, beyond a unit test | Test gap | The filter lived where the engine types were. | Filter moved into the service as a plain-object predicate, one test per guard; the co-op gate became a pure function with tests. |
| 12 | LOW | A release at disband start sends heroes into vanilla's fugitive state for about 3 days; the first adapter interface named `MobileParty` and failed `ServiceAndAdapterSignatures_NameNoTaleWorldsClass_ExceptTheShrinkOnlyBaseline` | Design and gate | Second pass and the gate. | Immediate teleport when the party is in a settlement, fugitive otherwise; the interface was dropped (nothing fakes it) and its class became `LordRetinueWorld`. |

Second-pass items accepted with a reason: a lost clan party makes every party of that clan release its retinue that day and retake over the following days (bounded churn, in the doc); a party with no troops left after the release (needs a party of only retinue heroes, no caller makes one); an unreadable co-op registry also disables the disband release (fails closed); role perks and raw skill can disagree on which hero is better (a hero's effective skill is not read).

## Root-cause pattern

**State the feature creates for the first time.** Findings 1, 2, 4, 5 and 8 are one failure: the design was written from what the feature does to a hero and a party, and no step listed what vanilla does with a hero in an AI roster or a role on an AI party. Each vanilla consumer holds an unstated precondition ("a lord party's only hero is its leader", "an AI party has no role holders"). The handover document and my plan both stated vanilla behavior as fact without a quoted line.

**A copied sibling.** Finding 3 repeats a lesson that is already on file. The rule existed in `coop-interop.md` and in `lessons/campaign-mechanics.md`; it did not reach the builder because no gate looks for it.

## Why each lens caught it, and what did not

- **Standards, Efficiency, Completeness, Design:** skipped by tier (tier 1 runs Data flow and Engine compatibility). Tests and gates caught finding 12 (the signature gate) and the count pins.
- **Engine compatibility** found 1, 2, 3, 5, 6 and a LOW set from the quoted vanilla source. **Data flow** found 1, 2, 3, 4, 7 and 11 by tracing what other code does with the new state. Their overlap on the three HIGH findings is the signal that the tier choice was right.
- **What did not catch them:** the unit tests, the build, the settings gates and `lint_docs`. All were green with all three HIGH defects present.

## Preventive actions

1. `docs/reviews/lessons/campaign-mechanics.md`: two new entries (vanilla readers of new state; a fallback slot is already filled) and a repeat note on the co-op entry of 2026-10-06.
2. One row in `.claude/rules/csharp-architecture.md`: list every vanilla reader and cleanup of state the feature creates before the first test.
3. FOLLOW-UP no issue: a gate that fails a new `CampaignBehaviorBase` under `Main/Features` which compares against `Clan.PlayerClan`, `MobileParty.MainParty` or `Hero.MainHero` without `IPlayerControlQuery` or a `local-peer-ok` marker. It needs a design (which existing behaviors it would flag) and a public issue needs the maintainer's word.
4. In-game checks owed (label `triage-needs-ingame` on #1122): the marriage repro (`campaign.marry_hero_to_hero` on a retinue party's leader, then wait two days: the hero must not stay listed in a garrison) and a party size tooltip before and after a Steward-best relative joins a high-Steward lord.
