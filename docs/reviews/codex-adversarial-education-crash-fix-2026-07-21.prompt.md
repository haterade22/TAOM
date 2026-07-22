ADVERSARIAL REVIEW: education-crash-fix (#354) -- age-8 child-education CTD fix + validator + PatchShield diagnostics change.

FEATURE: Player crash bundle 94c7b795 (TAOM v2.0.12, Bannerlord v1.4.7): clicking the child-education map notification for a lothlorien child at age 8 CTD'd with NullReferenceException. Root cause: lothlorien (and umbar, goblin, mistymountainorcs) had ZERO child_education_templates_stage_2_page_0_branch_{0-5}_<culture> NPCCharacters; the engine null-derefs the lookup result. Fix is data + a validator rule + a PatchShield exception-propagation change.

SCOPE -- review ONLY these uncommitted changes (git diff each). The working tree ALSO contains unrelated uncommitted work (LotrIssues C#, .claude harness files, CHANGELOG top entry for lotr-issues) from another session: EXCLUDE it entirely.
1. Main/_Module/ModuleData/taom_education_character_templates.xml -- +24 NPCCharacter blocks (4 cultures x branches 0-5)
2. Main/_Module/ModuleData/equipmentsets/taom_education_equipment_templates.xml -- +392 EquipmentRoster blocks (98 each for lothlorien, umbar, shaghana, abanissa; cloned from rivendell/gondor)
3. tools/taom_schema.py -- new _education_coverage() check (MISSING_EDUCATION_TEMPLATES ERROR) + run() wiring
4. tools/tests/test_validate_moduledata.py -- 6 new tests
5. tools/add_education_roster_cultures.py -- KNOWN_CULTURES 10 -> 14
6. Dependencies/Foundation/PatchShield.cs -- ShieldFinalizerVoid/ShieldFinalizerWithResult now return the ORIGINAL __exception on the non-swallow path (previously returned the TargetInvocationException-unwrapped inner exception, which Harmony's generated `throw` re-stamps, destroying the real stack). ShouldSwallow still unwraps for swallow-trinity classification only.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom, in taom_spcultures.xml): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, shaghana, abanissa, goblin, mistymountainorcs (all is_main_culture="true"; plus 8 is_bandit raider cultures)
Culture IDs (XSLT/vanilla, reused by TAOM kingdoms): vlandia=Rohan, empire=Dunland, empire_w=Gondor(kingdom only), aserai=Harad, khuzait=Easterlings/Rhun, sturgia=Dale, battania=Khand
NOTE: "rohan" is NOT a valid culture ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- git diff of the 6 files above (authoritative for what changed)
- docs/reviews/rca-education-crash-fix-2026-07-21.md (the investigation RCA)
- E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EducationCampaignBehavior.cs (v1.4.7 matches installed)
- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodCreator.cs (finalizer emission)
- tools/README.md section on validate_moduledata

KNOWN SUSPECTS -- CONFIRM or DISPUTE each with evidence:
S1. Template leakage: the 24 new NPCCharacters use occupation="Lord", is_template="true", age="45", culture="Culture.<main culture>". HYPOTHESIS: some engine or TAOM code enumerates CharacterObjects by occupation/culture WITHOUT filtering is_template (lord spawning, emissary pools, tournament participant pools, TAOM CustomBattles/SettlementGuards/VolunteerRecruitment). Grep TAOM Main/ and decompiled vanilla for occupation-based CharacterObject enumeration and determine whether template objects can leak into gameplay pools. The 10 pre-existing cultures' templates share this exact shape (precedent says safe) -- verify the precedent actually holds rather than assuming.
S2. PatchShield behavior change blast radius: finalizers are installed on ~505 patched methods process-wide. Returning the ORIGINAL exception (often a TargetInvocationException wrapper) instead of the unwrapped inner changes the exception TYPE seen by every upstream catch across the whole game + all TAOM/BUTR/MCM code when a reflection-invoked path throws. HYPOTHESIS: some catch somewhere type-matches the inner exception type (e.g. catch (NullReferenceException), catch-when filters, MCM/ButterLib handlers) and now misses because it receives the TIE wrapper. Grep Main/, Dependencies/ (including vendored ButterLib/MCM/UIExtenderEx sources under Dependencies/.vendor-source if present) and reason about vanilla TaleWorlds catch sites. Note: pre-PatchShield vanilla propagation ALSO delivered the TIE wrapper, so the new behavior equals vanilla -- verify that claim.
S3. Validator regex on taom_spcultures.xml: <Culture\b([^>]*?)/?> with re.S. HYPOTHESIS: an attribute value containing '>' would truncate the match; also verify the is_main_culture/id extraction cannot cross element boundaries on malformed input, and that the comment-masking preserves line numbers.
S4. Equipment roster superset: each culture got 98 rosters but the engine only ever requests ~65 ids (stage-2 branch_{0-5}/default and stage-5 page-1/2 are unreachable per EducationCampaignBehavior). HYPOTHESIS: unreachable rosters are harmless (no duplicate ids, no engine warning spam beyond what the 10 existing cultures already produce). Verify no id collides with anything else in the ModuleData registry.
S5. Stage-2 branch_child roster: engine line 188/207 requests child_education_equipments_stage_2_page_0_branch_child_<culture>. HYPOTHESIS: all 4 new cultures include it (the clone sources have 22 stage-2 rosters incl branch_child + branch_default). Verify by grep.
S6. Race/facegen for the education screen: new templates use race="elf"/"goblin"/"orc"/none. HYPOTHESIS: GauntletEducationScreen.CreateAgentVisual needs Monster (FaceGen.GetBaseMonsterFromRace) + action sets; elf/goblin/orc Monsters + as_<race>_facegen action sets exist in LOTRLOME_Armory (live game install at E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\). Verify against the LIVE files, not repo copies.

REQUIRED SECTIONS:
1. VANILLA CODE: paste the relevant EducationCampaignBehavior lookup + deref lines (GetSpecialCharacterForOption, GetSpecialCharacterPropertiesForPage/ForOption, GetChildEquipmentForPage/ForOption) and the Harmony MethodCreator finalizer emission lines as code blocks, from the ACTUAL files.
2. DEEP ANALYSIS -- concrete scenarios:
   a. Lothlorien child turns 8, player clicks notification: walk the full chain with the NEW data and state exactly what renders for each of the 6 options.
   b. A lothlorien child on an EXISTING save that already passed age 8 unresolved (notification pending in save): does the fix apply retroactively on load? Any save-compat risk from adding NPCCharacter/EquipmentRoster objects (MBObjectManager ids, save object registry)?
   c. Stage Year11/14/16 for the 4 cultures: confirm no further culture-keyed CharacterObject lookups exist that we did NOT cover.
   d. PatchShield: a mod patch throws MissingMethodException wrapped in TIE -- confirm it is STILL swallowed + unpatched (classification unwrap unchanged). A handler throws NRE via reflection invoke -- confirm the crash report now shows TIE + inner NRE with real frames.
3. CONFIG CROSS-REFERENCE: every Item./Culture./SkillSet./BodyProperty. id referenced by the +24 blocks resolves (vanilla SandBoxCore/Native for empire_* items, LOTRLOME_Armory mordor folder for sk_md_orc_*, TAOM_bodyproperties.xml, sandboxcore_skill_sets.xml).
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 ship-blocker / P2 should-fix / P3 nice-to-have), file:line, and the exact evidence. If a section yields nothing, write "No findings" -- do not pad.

QUALITY GATES:
- Confirm/dispute EVERY Known Suspect explicitly, with file:line evidence. No hand-waving.
- Paste real code blocks from both codebases (TAOM + decompiled vanilla). A review without code blocks is a failed review.
- Do not flag vanilla-matching behavior as a bug.
- Do not review the out-of-scope uncommitted files (LotrIssues, .claude, CHANGELOG lotr-issues entry).

PRIOR REVIEW LESSONS:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Save/Load apply-timing caught a CRITICAL patch-timing bug (#299).
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections when instructions allowed it.

Output your review to stdout (it is captured to docs/reviews/raw/codex-adversarial-education-crash-fix-2026-07-21.md).
