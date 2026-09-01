You are performing an adversarial code review of an uncommitted changeset in the TAOM Bannerlord mod repo at e:\repos\TAOM. Target Bannerlord v1.4.8. Be skeptical. Your job is to find defects that a green test suite and four green gates did not.

WHAT THE CHANGE IS

Players reported that enlisting in a lord's army got them armour and never a weapon. That was literally true: a slot census of Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml returned 374 armour elements and ZERO Item0-Item3 across all 84 rosters. Armour-only was the shipped contract, enforced in the generator (a slot filter), the coverage auditor (a hard fail on any non-armour slot) and four comments. The C# was never at fault: Main/Adapters/EquipmentRosterCatalogAdapter.cs:58 already iterated all 12 EquipmentIndex slots.

The fix is data plus tooling:
- Roster ids went from enlist_{culture}_{rank} to enlist_{culture}_{assignment}_{rank}. 266 rosters now: 250 culture cells of a possible 320 (20 cultures x 4 assignments x 4 ranks) plus 16 enlist_default_{assignment}_{rank}.
- ServiceAssignment (Infantry/Archer/Cavalry/Support) is used DIRECTLY in the Equipment namespace, not mirrored into a second enum.
- EnlistmentRosterResolver walks culture, then assignment (falling back to Infantry), then rank (descending).
- Kits carry weapons Item0-Item3. NO Horse, HorseHarness or Item4 at any assignment, cavalry included.
- The rank band became a HARD CAP in the generator (it was only a sort weight), with one rescue path when a culture would otherwise own no roster at a rank at all.
- Support kits carry exactly one melee sidearm and no shield.
- The issue ledger stays keyed on RANK alone: a role swap does not re-open a spent draw, and a save already at Sergeant gets nothing. Both accepted and documented.
- The cavalry reassign dialog line was reworded off "Give me a horse" and retranslated in 12 languages; the key was evicted from all 12 tools/translation_cache/*.json because that cache is keyed by STRING ID, not by English text.
- tools/audit_polearm_shield_parity.py matched lowercase <equipment> only, so it had never opened a standalone equipmentsets/ roster file. Fixing the casing surfaced 13 pre-existing shield+polearm findings across 10 rosters, now held in a KNOWN_FAILURES ratchet under issue #526.

A prior 11-dimension internal review already ran and its findings are FIXED. Do not re-report these; they are listed so you can verify the fixes are correct and complete rather than rediscovering them:
1. 15 Support rosters shipped with armour and zero weapons, because support_kit() returned an empty map when the donor had no OneHanded item and pick_donor emitted the cell anyway.
2. No gate asserted that a kit contains a weapon at all.
3. ASSIGNMENT_GROUPS mapped 3 of the 4 default_group values, so 23 HorseArcher troops belonged to no donor pool.
4. 18 (culture, assignment) chains emitted a byte-identical kit at two or more ranks.
5. 17 chains issued strictly worse armour on promotion.
6. Honourable discharge reclaims looted ammunition (documented, issue #527, not fixed here).

TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid, use "dolguldur".

READ FIRST

- docs/features/enlistment.md, the Equipment section
- docs/reviews/rca-enlistment-weapons-2026-09-01.md
- .claude/rules/moduledata-validation.md and tools/README.md "XML I/O convention"
- CHANGELOG.md, the two 2026-09-01 entries for #525 and #526

FILES CHANGED

C# production:
Main/Features/Enlistment/Equipment/EnlistmentRosterIds.cs
Main/Features/Enlistment/Equipment/EnlistmentRosterResolver.cs
Main/Features/Enlistment/Equipment/EnlistmentEquipmentService.cs
Main/Features/Enlistment/Equipment/IEnlistmentEquipmentService.cs
Main/Features/Enlistment/Equipment/EnlistmentRank.cs
Main/Features/Enlistment/Hooks/EnlistmentQuartermasterBehavior.cs
Main/Features/Enlistment/Hooks/EnlistmentAssignmentDialogBehavior.cs

Tests:
TAOM.Tests/Features/Enlistment/Equipment/EnlistmentRosterResolverTests.cs
TAOM.Tests/Features/Enlistment/Equipment/EnlistmentEquipmentServiceTests.cs
TAOM.Tests/Features/Enlistment/EnlistmentRosterCultureCoverageTests.cs
TAOM.Tests/Features/Enlistment/EnlistmentRosterSlotInvariantsTests.cs (new)
TAOM.Tests/Features/Enlistment/EnlistmentEquipmentCultureTests.cs

Tools:
tools/generate_enlistment_rosters.py (near rewrite)
tools/audit_enlistment_roster_coverage.py (rewrite)
tools/audit_polearm_shield_parity.py
tools/tests/test_audit_polearm_shield_parity.py
tools/promote_borrowed_cultures.py

Data and config:
Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml
Main/_Module/ModuleData/taom_enlistment_strings.xml plus 12 Languages/*/std_taom_enlistment_strings_*.xml
12 tools/translation_cache/*.json
Main/_Module/SubModule.xml

Related but NOT changed, and relevant to correctness:
Main/Adapters/EquipmentRosterCatalogAdapter.cs
Main/Adapters/PartyItemRosterAdapter.cs
Main/Features/Enlistment/Content/DischargeConsequenceService.cs
Main/Features/Enlistment/Equipment/PersistedEquipmentIssueLedger.cs
Main/Features/Enlistment/Content/Domain/ServiceContentRecord.cs

KNOWN SUSPECTS -- confirm or dispute each, with evidence from the code

SUSPECT 1: EnlistmentRosterResolver.Resolve has two sequential foreach loops over AssignmentChain(assignment), a generator method that is therefore enumerated twice. The first loop contains "if (string.IsNullOrEmpty(cultureId)) break;" INSIDE the loop body rather than as a guard before it. Trace this precisely for a null culture, an empty culture, and a whitespace culture. Does it probe anything it should not? Is the second enumeration of the generator correct? Is there any input for which the same roster id is probed twice, or for which a reachable roster is never probed?

SUSPECT 2: The generator now applies THREE independent suppression rules to a cell: the hard level cap, "kit identical to one already emitted at a lower rank in this chain", and "armour total below the highest already issued in this chain". Read tools/generate_enlistment_rosters.py main() and pick_donor(). Can these interact to leave a (culture, assignment) chain with NO rosters at all, or leave a culture with no roster at some rank while the rescue path does not fire? The rescue only triggers when ALL FOUR assignments came back empty for that rank. Prove or disprove that every request still resolves, and note that the guarantee is asserted in EnlistmentRosterCultureCoverageTests using the real resolver.

SUSPECT 3: The armour monotonicity floor in pick_donor is applied as a FILTER that yields entirely when nothing clears it ("if kept: candidates = kept"). Does that yield reintroduce the regression it exists to prevent, silently, in any cell? Also: armour_total sums the derive_armor_tiers "primary" stat per slot, which is one stat per item, not the item's full armour contribution. Is that a sound progression proxy, and can it rank two kits wrongly?

SUSPECT 4: support_kit now accepts OneHanded, then Polearm, then TwoHanded. A Support soldier issued a two-handed axe is a rear-echelon trooper with a battlefield weapon. Read Main/Features/Enlistment/BattleFormationPolicy.cs and Main/Features/Enlistment/Content/AssignmentSkills.cs and judge whether that contradicts the Support design, and whether "one weapon and no shield" is still a meaningful distinction from Infantry.

SUSPECT 5: The ledger is keyed on rank alone while the roster is keyed on (culture, assignment, rank). Enumerate the consequences. A player who draws at Recruit as Infantry, swaps to Archer, and is promoted to Soldier draws the ARCHER soldier kit. Is any state inconsistent? Read PersistedEquipmentIssueLedger.cs and ServiceContentRecord.cs and confirm the assignment ordinal survives a save round trip and that a save written BEFORE this change deserialises to a valid ServiceAssignment rather than a garbage ordinal.

SUSPECT 6: EnlistmentRosterIds.AssignmentToken has a default arm returning "infantry" for an out-of-range ServiceAssignment. AssignmentChain then compares TOKENS to decide whether to yield Infantry as a second step. Verify this is correct for (ServiceAssignment)99 and that it cannot cause a double probe. Then ask the harder question: is defaulting to infantry right at all, versus refusing to resolve?

SUSPECT 7: The KNOWN_FAILURES ratchet in tools/audit_polearm_shield_parity.py is keyed on (roster id, item id). Can it suppress a NEW finding? Consider a roster already in the ratchet that later gains a SECOND unusable polearm, and a roster id that is reused. Also verify the stale-entry check runs when the matched list is EMPTY (that was a real bug, fixed mid-session).

REQUIRED SECTIONS

1. VANILLA CODE. Decompile and paste as code blocks, from the INSTALLED v1.4.8 DLLs, not from any decompiled dump:
   - TaleWorlds.Core.EquipmentIndex (confirm ExtraWeaponSlot=4, Horse=ArmorItemEndSlot=10, HorseHarness=11, NumEquipmentSetSlots=12)
   - TaleWorlds.Core.Equipment, the EquipmentIndex indexer and any slot-fit validation
   - MBEquipmentRoster deserialisation, to confirm a roster with weapons and no mount is legal
   - Equipment.GetEquipmentIndexFromOldEquipmentIndexName or equivalent, to confirm what XML slot name "Item4" maps to. TAOM comments and a new test call Item4 "the banner slot". Confirm or dispute that description.
   - Anything that enumerates ALL MBEquipmentRosters, to confirm adding ~180 rosters cannot perturb vanilla hero or aging behaviour.

2. DATA ANALYSIS of Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml. Parse it. Confirm 266 rosters, no duplicate ids, exactly one EquipmentSet each, no Horse/HorseHarness/Item4, every roster carries at least one weapon, no chain repeats a kit, no chain regresses in armour. Then look for what the gates do NOT check: a kit whose items belong to a visibly different culture than the roster id claims (the #427/#431 defect class, e.g. sk_gd_ Gondor items in a vlandia Rohan roster, or human items in a goblin/orc/dwarf/elf roster), a bow with two quivers, duplicate item ids within one roster, a kit that is absurd in play though schema-valid.

3. CONFIG CROSS-REFERENCE. Every culture token in a roster id must be a real runtime culture StringId per the cheatsheet above. Every item id must resolve. Check the 16 hand-authored enlist_default_* rosters especially: they are the resolver's last resort, so a broken id there is a silent empty kit.

4. LOCALIZATION. The English string taom_enlist_reassign_cav must be identical in the inline default in EnlistmentAssignmentDialogBehavior.cs and in taom_enlistment_strings.xml. All 12 language files must carry a translation that no longer promises a horse, and each of the 12 translation_cache entries must match its language file exactly, or the next translator run silently overwrites shipped text.

5. FINDINGS OR OBSERVATIONS. If you find nothing, say so plainly rather than padding. Rank by severity. For each finding give file, line, the concrete failure path, and the minimal fix.

QUALITY GATES

- Verify every "missing" claim by grepping before asserting it.
- A passing test suite is not evidence of correctness. Four gates and 7770 tests pass on this changeset already.
- Do not report style preferences.
- State file and line for every finding.

PRIOR REVIEW LESSONS

SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Looking at code NOT in the diff caught a discharge bug.
FAILURES: Codex has assumed empire=Rohan (it is Dunland). Codex has flagged vanilla-matching code as bugs. Codex has skipped hard sections. Codex has reported findings the changeset deliberately decided against (here: no mounts, rank-keyed ledger, no save migration, 70 absent cells, the 13 ratcheted polearm pairs) as if they were defects. Those are decisions, recorded in the CHANGELOG and the RCA. Disputing a decision on its merits is welcome; reporting it as an unnoticed bug is not.
