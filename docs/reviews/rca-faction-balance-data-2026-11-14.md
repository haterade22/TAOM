# RCA: faction balance data change (#1283, 2026-11-14)

## Top-line

`/deep-review` of the #1283 data change ran at tier 2: the data-flow lens at high effort. It confirmed one MEDIUM and five LOW findings, and each was re-read in the source before it was fixed. None changes the shipped data, and all six were fixed before the commit. Findings 2 to 5 repeat lessons recorded within the last week. Two errors in the builder's own plan were caught before the lens ran, both by reading a feature doc late. A Codex pass ran after the commit, against the commit, and added three LOW rows (7 to 9).

The change: the Hostile row between Dunland (`empire`) and Gondor (`empire_w`) is removed from `diplomacy.json`, the `mordor` survival row (-0.2) is removed, and `mirkwood` gets 0.4, in `battle_balance_config.json` and in the compiled defaults.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The changelog line in `docs/features/diplomacy.md` and the issue named one route to a Gondor-Dunland war after the change: either realm's own declaration. A second route exists. A realm allied with one of them and at war with the other can call it in, and for an AI realm the caller's vote alone starts the war (`ProposeCallToWarAgreementDecision.ApplyChosenOutcome`, v1.5.7). Rivendell and Arthedain are Hostile to Dunland and Natural with Gondor. The agreement lasts 42 days. | Engine reader of a removed state | The builder traced the call to war through Gondor's one Permanent ally, Rohan, and stopped. A Natural row adds 500 to the alliance score, so those realms can ally too. The Hostile tier had also kept the pair out of three vanilla ally filters through `IsAtConstantWar`, and no step listed those readers. | Doc line and issue corrected. New lesson in `lessons/campaign-mechanics.md`. Whether such a vote passes is UNVERIFIED: `GetScoreOfCallingToWar` refuses a caller stronger than its target, and no campaign was run. |
| 2 | LOW | The Configuration section of `docs/features/diplomacy.md` still gave 79 Hostile pairs and stated the Hostile rule with no Dunland exception. | Stale prose copy of data | Only the changelog was updated. The count carries a `measured:` command, and it was not re-run. | Re-measured (78), re-dated, exception sentence added. Repeat of `lessons/misc.md`, "A retuned value is searched in every prose form before the retune is called done". |
| 3 | LOW | The MCM hint of `EnableCulturalSurvivalBonuses` (`Main/Features/TaomSettings.cs`) still said "Mordor -20%". | Stale player-facing copy | The search for the old value covered docs and tests, not C# string literals. | The hint no longer restates config values, so a data edit cannot stale it again. Same repeat as 2. |
| 4 | LOW | A test comment, the changelog line in `docs/features/battle-balance.md` and the issue said that no low-level Mordor troop ever survived a wound. That holds only for a cut or pierce blow in a party with no Medicine skill. `DefaultPartyHealingModel.GetSurvivalChance` returns 1 for a blunt blow that cannot kill, and `AddSurgeonSurvivalBonus` adds the surgeon's or the leader's Medicine effect to the denominator, a quarter of it outside a player battle. | Effect claimed from part of a method | The builder read the method, took the surgeon helper as zero without opening it, and passed over the blunt early return. | All three texts now state the condition. Repeat of `lessons/campaign-mechanics.md`, "A limit read from an engine formula covers every branch of that method". |
| 5 | LOW | Leftover labels: a `DataRow("mordor", 0f)` under a test named for defined cultures, a "Mordor: -20%" comment on a math row, and the "Mordor armies shrug off losses" intent line in `battle-balance.md`. The test that pins Mordor's missing penalty also failed on an explicit zero row. | Stale labels | Same as 2. | Row removed, comment and intent line reworded, the assertion now fails only on a negative value. |
| 6 | LOW | Two changelog lines and a test comment were dated 2026-11-15. | Wrong date source | The date came from the UTC timestamps of the work claims. The machine's local date was 2026-11-14. | Dates corrected. One-off. |

Codex pass (`high`, on commit 5832fc5c6): no P1 or P2. Three P3 findings in the change, each re-read in the source, and two gaps that predate it:

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 7 | LOW | The survival texts named one condition, no Medicine skill in the party. `GetSurvivalChance` also adds the enemy party's Doctor's Oath bonus to the denominator (lines 72 to 74). | Effect claimed from part of a method | The fix for finding 4 covered the two branches the lens named and did not re-read the method for a third. | Changelog line and test comment now name both sides. The commit body of 5832fc5c6 keeps the narrower wording. Second repeat of the lesson named in finding 4. |
| 8 | LOW | The commit body of 5832fc5c6 says an ally can call Gondor in "for 42 days at a time". Only the agreement and its peace block end after 42 days; `EndCallToWarAgreement` makes no peace, so the war goes on as an ordinary war. | Wording that implies an effect | The builder read `EndCallToWarAgreement` and shortened the sentence past what it does. | The follow-up commit body states it correctly. The docs and the issue already said "the agreement lasts 42 days". |
| 9 | LOW | `docs/modding/configs-factions-and-world.md` still gave 153 rows and 79 Hostile. | Stale prose copy of data | The search for the old count used the words "Hostile pairs" and missed a table row that lists the totals. | Corrected to 152 and 78. Same repeat as finding 2. |

- FOLLOW-UP (no issue: predates this change, for the maintainer's batch): `BattleBalanceConfigProvider.ValidateConfig` reverts an out-of-range survival value to 0, not to its compiled default, and prints no summary warning (`config-providers.md` items 5 and 6).
- FOLLOW-UP (no issue: predates this change, for the maintainer's batch): no test asserts that both kingdom ids of every `diplomacy.json` row resolve (`xml-data.md`, "Config ID Cross-Reference"). Codex cross-checked the shipped rows and found no bad id.

Caught by the builder before the lens ran, recorded for the pattern:

- **A.** The plan removed Gondor's four towns from Dunland's priority list in `army_targeting.json`. The edit was applied and then reverted, when `docs/features/army-targeting.md` showed that a 2026-08-21 prune of list entries had been reverted for this reason: the priority boost decays over the length of a list, so a shorter list lowers the boost on the entries that stay.
- **B.** The claim "auto-resolve reads tier only" came from a grep of four lines of `TaomMilitaryPowerModel`. The two models also apply the mounted factor, hero level, the leader's captain perks, terrain and morale, and `docs/modding/balance-levers.md` already says so.

## Root-cause pattern: the docs were read after the edit

Findings 2 to 5 and both pre-lens errors share one cause. Each subsystem in this change (diplomacy, battle balance, army targeting) has a feature doc and lesson entries that state the trap, and the builder read them only while updating them. `orientation.md`, "Lessons", already requires a list of a subsystem's lesson headings before the first edit. The work began as a read-only review of an outside handoff and moved into implementation without that step.

Finding 1 is a different class: a change that removes state. `csharp-architecture.md`, "Readers before writers", covers state a feature creates for the first time. Nothing covered the readers that lose a state when a data row goes. The new lesson does.

## Why each reviewer missed or found these

- **Builder self-check before the lens:** checked the code readers of both tables and the culture id of the new key. It did not check the prose copies, and it checked the allies of the pair only through the Permanent row.
- **Agent 5, data flow:** found all six.
- **Lenses 1, 2, 3, 4, 6 and 7:** skipped by tier. The change holds no XML and no script.
- **Codex:** found 7 to 9 and the two older gaps; it confirmed the lens's traces of the diplomacy readers, the old-save case and the survival table.

## Lessons

- New: `docs/reviews/lessons/campaign-mechanics.md`, "Taking a pair out of constant war reopens vanilla's ally filters: list who can call each realm in".
- No new rule for findings 2 to 5. The rules exist and were not followed.

## Owed

- In game: the AI-only campaign named on #1283 (label `triage-needs-ingame`). It now also records whether an ally calls Gondor into a war with Dunland after day 44, and whether the two realms ally before day 30.
- FOLLOW-UP (#1283): if that run shows a called war, decide whether the pair needs a relationship tier that refuses AI wars without making an alliance.
