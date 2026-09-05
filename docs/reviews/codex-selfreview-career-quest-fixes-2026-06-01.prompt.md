# Codex Self-Review -- TAOM Career Quest fixes (Phase 3)

You already adversarially reviewed the TAOM career-quest feature and found 5 findings. This is the SELF-REVIEW pass: review ONLY the fixes that were just applied for those findings, looking for NEW bugs introduced by the fixes (regressions, wrong API, edge cases). Do NOT re-review the whole feature. Repo root: c:/Users/mikew/source/repos/TAOM. Bannerlord 1.4.5 (verify on installed DLLs, not a dump).

## The 5 fixes to scrutinize

F1 (MED) -- `CareerQuest.OnHeroKilled` (Main/Features/CareerSystem/Quests/CareerQuest.cs). Now gates `KillEnemyLords` on the victim being an enemy:
```
if (killer != Hero.MainHero || victim == null || !victim.IsLord) return;
var playerFaction = Hero.MainHero.MapFaction;
if (playerFaction == null || victim.MapFaction == null || !victim.MapFaction.IsAtWarWith(playerFaction)) return;
Bump(CareerQuestObjectiveType.KillEnemyLords, null);
```
SCRUTINIZE: Is `Hero.MapFaction` the right "faction at war" accessor on 1.4.5 (vs `.Clan` / `.Clan.Kingdom`)? Does `IFaction.IsAtWarWith(IFaction)` reflect the war state at the moment of the kill event (HeroKilledEvent timing)? Any case where a legit enemy-lord kill is now MISSED (false negative) -- e.g. a clanless/factionless lord, a mercenary, a rebel, or a lord whose faction was just defeated so MapFaction is null at kill time? Is rejecting null MapFaction the right call (could a valid kill have null MapFaction)?

F2 (LOW) -- `CareerQuestCampaignBehavior.Offer` now wraps `InformationManager.ShowInquiry` in try/catch that sets `_offerPending = false` on exception. SCRUTINIZE: correct + no double-reset issue.

F3a (MED) -- `CareerQuestConfigProvider.ParseObjective` now rejects a `VisitSettlementType` whose `param` is not Town/Castle/Village (case-insensitive). SCRUTINIZE: does `CareerQuest.SettlementTypeName(settlement)` emit EXACTLY "Town"/"Castle"/"Village" (matching case + spelling) so a valid authored objective can actually progress? Cross-check the producer string against the validator set.

F3b (LOW) -- `CareerQuestConfigProvider.ParseReward(el, questId, questTier)` now coerces an `UnlockTier` reward's amount to the quest's tier (with a warning) when they differ. SCRUTINIZE: is coercion the right call (vs reject)? Any case where the coercion is wrong?

F3c (LOW) -- `CareerQuest.OnStartQuest` now warns when a `GrantItem` reward's item id doesn't resolve via `MBObjectManager.GetObject<ItemObject>`. SCRUTINIZE: correct + no false warning for a valid id.

## Cross-cutting
- Confirm `CareerQuest.SettlementTypeName` (the producer for F3a) returns strings that EXACTLY match the F3a validator allow-list -- a mismatch would make every VisitSettlementType objective silently un-completable. This is the highest-value check.
- Confirm the F1 war-check doesn't break the `taom_career_quests.xml` proof-of-life quest (which uses WinBattles + SkillThreshold + RenownThreshold, NOT KillEnemyLords) -- i.e. no unrelated regression.

## Output
FINDINGS table: # | Severity | File:line | Issue | Fix. If the fixes are correct, say so explicitly per fix (CONFIRMED CORRECT). Use installed-1.4.5 decompiles for any API claim; say UNVERIFIED rather than guess.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
