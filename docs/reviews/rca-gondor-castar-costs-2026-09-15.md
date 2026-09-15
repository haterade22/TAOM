# RCA: Gondor elites cost Castar (#600), deep review of 2026-09-15

**Scope.** The `/deep-review` pass (six agents: standards, engine compatibility, efficiency,
completeness, cross-system data flow, tooling correctness) over the #600 changeset: 16 Gondor troops
at level 41 and above gaining `upgrade_cost` and `daily_upkeep` in `troop_resource_costs.xml`, the
second Black Numenorean upkeep halving, two shipped-data tests, the generator table, the docs.

**Top line.** Standards, efficiency and completeness passed clean; the three Patch26 targets verified
against the installed v1.5.3 DLLs. Four findings survived verification: one design gap in the new
data (fixed, with a third test), one stale generator table that would have written rows the emissary
drops (fixed), and two pre-existing defects the change makes visible for Gondor for the first time
(recorded, not fixed here). No HIGH in the shipped code; the design gap would have been HIGH had it
reached players, because it left the two most iconic Gondor troops free to recruit. Codex (GPT-6-Astra
at ultra, review 112, about 25 minutes) then took the post-fix staged set and refuted the recruit gate
the fix relied on: one MEDIUM (the Confirm hotkey bypasses the greyed Done button, fixed with a prefix
on `ExecuteDone`) and one LOW (a stale sentence in the CHANGELOG, fixed). It confirmed the
seed-versus-growth reading from the installed decompile, refuted the reward-debit suspect (the vassal
reward and direct transfers raise no recruitment event), found no growth route into the sixteen, and
added Cair Andros as a Ranger seed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED (HIGH if shipped) | The Gondor block's justifying comment said "L41+ is upgrade-only (MaxVolunteerTier 6 stops a notable's slot at level 35), so upgrade_cost covers every path in". False for TAOM: `TaomVolunteerModel.GetBasicVolunteer` seeds an EMPTY slot from `VolunteerRecruitmentService` pools, and vanilla's `RecruitmentCampaignBehavior` applies `Tier < MaxVolunteerTier` only to the growth branch of an occupied slot (`:250`), never to the seed (`:244-248`). `gondor_ithilien_ranger` (L51) is a 10 percent pick at Minas Tirith and both Osgiliaths in the live `gondor.json`; `gondor_mt_fountain_guard` (L46) is in `clan_empire_west_1`'s pool and the vassal reward. Neither carried a `recruit_cost`, so both were free from a notable and only ever cost upkeep. | Data flow | The author took the `MaxVolunteerTier` reasoning from the Ironpass comment in the same file, which is correct for the RAM (its pooled rung is the L16 herder, which climbs) and wrong for a capstone that is itself pooled. The new test asserted `upgrade_cost > 0`, the thing the author had decided to add, not the thing the design needed (a charge on every acquisition path). Nobody grepped the recruitment pools for the 16 ids. | Both rows carry `recruit_cost` equal to their upgrade cost (6, 5; first cut 45 / 28 at the emissary band, lowered the same day because the attribute is also charged per unit on ungated prisoner recruits, #563). `EveryVolunteerPooledUpkeepTroop_CarriesRecruitCost` unions `AllPooledTroopIds()` with every `recruitment_pools/*.json` and requires a `recruit_cost` on every pooled row with upkeep. The comment now states the seed rule. Lesson appended to `lessons/campaign-mechanics.md`. |
| 2 | MED | `tools/wire_black_numenorean_troops.py` said "merchant_cost is deliberately omitted on every row ... this is a lord-party-only line that is not offered" and its `tiers` table wrote none. Since 2026-09-11 all 13 ids are Mordor `<CultureOffers>` and the shipped rows carry 6/8/12/18/28. A fresh run would have written 13 rows the emissary loader drops with a warning. Masked today by the idempotency short-circuit. | Tooling | The 2026-09-11 rescale edited the XML by hand and never returned to the generator; the #600 edit synced the upkeep column and stopped there, because the diff it was reviewing did not contain the comment. | `tiers` is a 3-tuple with `merchant_cost`, the writer emits it, the comment states the offer. A script asserted the table matches the shipped rows on all three fields for all 13 ids. |
| 3 | MED, recorded | `PartyUpgradeResourceCheckHook.GetUpgradeCost` returns the raw config cost to `PartyCharacterVM_InitializeUpgrades_Patch` (the troop-tree hint and its affordable count), while `ClampUpgradeCount` and `QueueUpgradeSpend` apply the career `SpecialResourceUpgradeCostModifier`. A player with that passive sees a hint computed at the undiscounted cost. Pre-existing; invisible for Gondor until it had upgrade rows. | Parallel-method consistency | Outside the diff. The 2026-09-11 review compared the three THRESHOLD gates and not the four COST derivations. | Recorded here and in the CHANGELOG; a follow-up issue is the maintainer's call. Fix shape: route the hook through `GetEffectiveUpgradeCost`. |
| 4 | LOW, recorded | Desertion takes `Math.Max(1, (int)(count * 0.1f))` per upkeep-troop TYPE (`SpecialResourceService.cs:451`). A Gondor player at zero Castar holding one of each of the 16 types loses 16 a day; the Black Numenorean line already has this shape with 13 types. | Balance | Existing rule, amplified by 16 new types. | Recorded in the CHANGELOG. If it reads as a defect in play, the fix is a per-party floor rather than a per-type one. |
| 5 | MED (Codex M1) | The recruit gate was a UI property. v1.5.3's `GauntletMenuRecruitVolunteersView.OnFrameTick:90-94` sends the Confirm hotkey to `RecruitmentVM.ExecuteDone` without reading `IsDoneEnabled`; `ExecuteDone` checks party capacity and `OnDone` rechecks gold only, so with 40 Castar and a 45 Ranger in the cart the hotkey recruited the Ranger and the charge floored at 40; at zero it recruited for nothing. Pre-existing for the rams and creatures since Patch51 shipped (2026-06-19), exposed to Gondor by these rows. | Gate at the wrong boundary | Patch51 mirrored vanilla's gold gate, which sets the same flag, and stopped there; vanilla protects its own requirement AGAIN in `OnDone`, and nobody asked what else calls `ExecuteDone`. Six deep-review agents read the patch and the VM and none opened the VIEW that owns the hotkey. | `RecruitmentVM_ExecuteDone_Patch` (same category) re-runs the cart verdict as a prefix on `ExecuteDone` and skips the commit with the same line in red; `RecruitCartGrouping` is the shared pure fold; `RecruitGatePatchTests` pins grouping, category and target; `HarmonyPatchBindingTests` resolves the target on the installed DLL. Lesson in `lessons/localization-ui.md`. |
| 6 | LOW (Codex L1) | The CHANGELOG entry's first paragraph still said "L41+ is upgrade-only, so `upgrade_cost` covers every path in" while its third paragraph explained why that was false. | Documentation | The fix edited the paragraph that described the fix and not the one that carried the premise. | Sentence replaced in the worktree and the index. |

## Root-cause pattern

Finding 1 is the one that matters. The change added a cost on the path the author was thinking about
(the party-screen upgrade) and wrote a test that proved that cost was present. The design needed a
cost on every path a player can take to the troop, and the file already documented two of them
(`upgrade_cost` on the upgrade, `recruit_cost` on the volunteer pick) with the rams as the worked
example. The tell was a sentence in the comment starting "so upgrade_cost covers every path in": a
claim of coverage made from one path. The fix that generalises is not "check the pools next time" but
the test now in place, which asks the file rather than the author.

Finding 2 is the sibling of the 2026-09-11 lesson "a cost row is not an upkeep row": a generator whose
table described the data as it was when the script was written, with no check that it still did.

## Codex, review 112

Handed the staged set after the deep-review fixes with six suspects. It confirmed suspect 1 (prisoner
recruits are charged per unit; the 45 / 28 asymmetry is the documented `recruit_cost` semantics and a
DESIGN choice, not a defect), refuted suspect 2 with the decompiled reward path, confirmed suspect 3 and
found no growth route, and turned suspect 4 into M1 by reading the VIEW: `GauntletMenuRecruitVolunteersView.OnFrameTick`
calls `_dataSource.ExecuteDone()` on the Confirm hotkey, `ExecuteDone` checks only party capacity, and
`OnDone` rechecks only gold with a `Debug.FailedAssert` that "the checks should happen before". It ran
the generator's fresh-file path through four fixtures and the config and storage arithmetic in memory,
and said it did not build or run MSTest. M1 is a real pre-existing defect: every troop with a
`recruit_cost` (rams, elephant, spider, Mumakil) has been recruitable past the gate by hotkey since
Patch51 shipped.

## Why each agent found or missed these

- **Data flow (agent 5)** and **compatibility (agent 2)** found finding 1 independently, because both
  were asked the direct question ("is any of the 16 reachable without an upgrade?" and "verify the
  MaxVolunteerTier claim") and both opened the installed decompile of `RecruitmentCampaignBehavior`
  rather than the comment. Agent 5 also found finding 3 by laying the four cost derivations side by
  side, and finding 4 by reading `CalculateDesertion` against the new type count.
- **Tooling (agent 6)** found finding 2 because it was told to read the whole script, not the diff.
- **Standards, efficiency, completeness** passed the change and were right to: none of the four
  findings is a standards, performance or completeness defect of the diff itself.
- **The author** missed 1 and 2 for the reason in the pattern above, and would have missed them
  without the review, because the tests were green and the validator was clean.
- **Codex** found 5 because it was asked what happens on Confirm and opened the class that owns the
  hotkey, which none of the six Claude agents did: their prompts named the VM and the patch and not
  the view. A gate review that stops at the flag the gate sets has reviewed the flag.

## Feedback memories to codify

None new. The durable rules go to `docs/reviews/lessons/campaign-mechanics.md` (finding 1, how TAOM's
volunteer model seeds slots) and `docs/reviews/lessons/localization-ui.md` (finding 5, a disabled button
is not a gate; gate the commit).
