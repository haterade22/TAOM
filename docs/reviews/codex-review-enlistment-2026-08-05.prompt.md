You are an adversarial code reviewer for TAOM, a Mount & Blade II: Bannerlord v1.4.7 total-conversion mod (LOTR). Your job is to find REAL bugs that shipped past a 5-agent internal review. Be skeptical: assume the author was over-confident. Do not praise. Report only defects you can point at with file:line evidence.

## What to review

Two commits on branch `bannerlord-1.4.5`: `25a3340c` and `b1852a7a` — a native rewrite of two donor mods into:
- `Main/Features/Enlistment/**` (serve as a soldier in a lord's party: state machine, attachment, discharge, menus, battle interception, content systems, duties, equipment)
- `Main/Features/FieldCommission/**` (promote a troop into a companion)
- New adapters in `Main/Adapters/` (ArmyRhythmProbe, HeroSkillXp, GoldTransfer, EquipmentRosterCatalog, PartyItemRoster, TroopRosterQuery, HeroCommission, InquiryPresenter, DutyWorld, Inquiry, CommanderLord, MobilePartyAttachment, Encounter, GameMenu)
- Modified: `Main/Adapters/WarEventSnapshotAdapter.cs`, `Main/Features/CareerSystem/Quests/CareerQuest.cs`, `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs`, `Main/IoC.cs`, `Main/SubModule.cs`
- Data: `Main/_Module/ModuleData/enlistment/*.json`, `equipmentsets/taom_enlistment_equipment.xml`, `field_commission/field_commission_config.json`, `taom_enlistment_strings.xml`

Use `git show 25a3340c`, `git show b1852a7a`, and read the working tree. TAOM conventions live in `CLAUDE.md` and `.claude/rules/*.md` — read `csharp-architecture.md`, `adapters.md`, `harmony-patches.md`.

## Known suspects — verify each, do not take the author's word

1. **`ServiceRewardService.PayDailyWage`** was rewritten after a HIGH bug (mint mode paid arrears through BOTH the commander transfer and the mint). Re-derive the arithmetic from scratch for: solvent/insolvent commander, mint mode vs commander mode, partial transfer, arrears at/over cap, wage 0, negative inputs. Prove the ledger is conserved (owed − delivered == new debt, clamped) or find the case where it isn't.
2. **State machine** (`Main/Features/Enlistment/Domain/EnlistmentTransitionTable.cs` + `EnlistmentStateMachine`): find a reachable sequence of campaign events that strands the player's party hidden+inactive with no service record, or that leaves `EnlistedBattle`/`Discharging` persisted. The discharge pipeline claims to always restore presence — try to break that.
3. **`Patch66` menu guard** (`GameMenuManagerSetNextMenuPatch`): the redirect fires only in `EnlistedAttached` and returns menus to `taom_enlistment_service_wait`. Find a menu flow (quest, incident, encounter, settlement, naval `port_menu`) where this either eats a menu the player needs or fails to redirect one that breaks service state. Check `EncounterGameMenuBehavior`'s 43 menu ids against the 11-id config list.
4. **Duty engine** (`Main/Features/Enlistment/Duties/**`): spawned looter parties get ids `taom_enlist_duty_*`. Enumerate EVERY exit path (complete, expire, cancel, discharge, commander death, save/load, player death, mod uninstall) and find one that leaks a spawned party or leaves `ActiveDuty*` fields pointing at a destroyed object. Also check the `DeliverFood`/`CollectFood` item math against `ItemRoster` semantics.
5. **`EnlistmentMeritMissionBehavior`** is `: MissionLogic` registered unconditionally. Verify it cannot NRE or mis-sample in: a mission with no `MainAgent`, a mission that ends during deployment, a hideout/arena/tournament mission, or a co-op client. Check `Mission.Agents` iteration cost per 2s sample.
6. **FieldCommission** (`Main/Features/FieldCommission/**`): the donor had 8 bugs; verify each fix actually holds, especially merit deduct-on-completion (can a player decline and lose merit? can they double-promote from one merit bank?), the `TextInquiryData` ctor argument order, and the race allow-list's fail-closed behavior against `IRaceManager.GetRaceNameFromId`'s "human" fallback.
7. **Co-op**: every world mutation should gate on `ICoopSessionProvider.IsAuthority`. Find an ungated one. Also check `IEnlistmentStateQuery` null-object registration ordering in `Main/IoC.cs`.
8. **NaN/engine-float gates** (`.claude/rules/csharp-architecture.md` "Engine-Float Decision Gates"): every comparison on an engine float or day value must FAIL into the safe branch on NaN. Check `ClassifyLeaveReason`, contract expiry, grace expiry, `WagePolicy`, `BattleMeritScorer`, `ArmyRhythmProbeAdapter` food days, `FieldCommissionMeritService` ratio, `AssignmentService.CooldownRemaining`, and every float→int cast.
9. **SyncData**: sections `_taom_enlistment`, `_taom_enlistment_content`, `_taom_fc_merits`, `_taom_fc_promotedHeroes`. Check round-trip fidelity, the `_justLoadedFromSave` guard against the same-process save→load→new-campaign path, and whether any section can exceed 32KB (TAOM had a save-corruption RCA at that threshold).
10. **Known limitation to confirm, not re-report**: the equipment issue-ledger is in-memory (documented) — but check whether that can cause anything WORSE than one extra kit draw per rank after a restart (e.g. duplicated payoff debt, ledger/record desync).

## Engine verification is mandatory

The installed game is at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`. Decompiled v1.4.7 source is cached at `C:\Users\mikew\.taom-src\v1.4.7\`. For ANY claim about TaleWorlds API behavior, read the decompiled source — do not assume. Signatures the author verified (spot-check a few): `GameMenuManager.SetNextMenu`, the 4 `LordConversationsCampaignBehavior` conditions, `MobileParty.SetMovePatrolAroundSettlement(Settlement, MobileParty.NavigationType, bool)`, `BanditPartyComponent.CreateLooterParty`, `HeroCreator.CreateSpecialHero`, `AddCompanionAction.Apply`, `TextInquiryData` ctor, `CampaignEvents.HeroPrisonerTaken/Released`.

## Output format

For each finding:
- **Severity**: P1 (crash/save-corruption/progression-loss) / P2 (wrong behavior) / P3 (smell)
- **File:line** and the exact code
- **Concrete failure scenario**: the sequence of player actions or campaign events that triggers it
- **Why the internal review missed it**
- **Minimal fix**

If you find nothing at a given severity, say so plainly. Finish with a one-paragraph verdict on whether this is safe to put in front of players given that NONE of it has been run in a live game yet.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
