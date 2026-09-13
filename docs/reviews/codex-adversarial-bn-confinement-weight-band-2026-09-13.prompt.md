ADVERSARIAL REVIEW: Black Numenorean confinement (#584) and the troop weight level-41 band (#585). Bannerlord 1.4.8 total conversion, repo E:\repos\TAOM, branch bannerlord-1.4.5.

Your job is to break this change. Try to refute every claim below before agreeing with it. Report only what you verified by reading source, decompiled engine code, or running a read-only command. Do NOT modify any file. Do NOT run git commands that change state (no add, commit, stash, checkout, reset).

IMPORTANT SCOPE NOTE. The working tree also contains OTHER sessions' unrelated in-flight edits (a troop skill rebalance across troops_*.xml, kingdom-armour tooling, a SmartCavalryAI test and a SmartCavalryMaxLineUpSeconds MCM property in TaomSettings.cs). Ignore those. Review ONLY the files listed under FILES IN SCOPE. For Main/Features/TaomSettings.cs the in-scope change is exactly one line: the HintText of EnableTroopWeight (about line 40).

WHAT CHANGED (two GitHub issues)

#584 Black Numenoreans field only for the two houses, Sauron and the vassal reward.
The mordor_num_* line (13 troops, troops/troops_mordor.xml lines ~3546-4169, all Culture.mordor, no volunteer pool) was sprinkled as 13 stacks (initiate 0/2, twelve at 0/1) into all 13 non-house Mordor clan lord templates (kingdom_hero_party_mordor_empire_south_N_template for N in 2..8 and 10..15) and the culture default kingdom_hero_party_mordor_template, plus one mordor_num_infantry 1/1 stack in patrol_party_mordor_template_level_3. All 183 stacks were removed. Then tools/rebalance_party_template_maxes.py --apply rescaled max_value on 120 stacks so each edited lord template sums to the 260 Mordor ceiling again (min_value untouched). Kept unchanged: kingdom_hero_party_mordor_empire_south_1_template (clan Dolgubeth, owner lord_1_14 Mouth of Sauron, 93 percent BN), kingdom_hero_party_mordor_empire_south_9_template (clan Wawrim, 93 percent BN), kingdom_hero_party_mordor_sauron_template (per-hero override for lord_1_17 via lord_party_templates/lord_party_templates.json, Patch88), vassal_reward_troops_mordor (one mordor_num_vet_infantry 1/1, the player's route into the line; also bound by Culture battania = Khand via spcultures.xslt). tools/wire_black_numenorean_troops.py had LORD_TEMPLATES cut to the two houses and its patrol EXACT entry removed, because it adds any stack a template lacks and would have re-sprinkled on the next run. Deep review then found that tools/generate_clan_heraldry.py's upsert_party_template guard (refuse a spec that would drop a live troop id) went blind for the 13 templates once the ids it keyed on were gone, so it now also refuses a spec whose max_value sum is below the live template's; 19 of the 21 clan_heraldry/*.json specs refuse on a dry run (bandits and khand pass).

#585 Troop weight 3.0 for every level 41+ elite.
Main/_Module/ModuleData/TroopWeights/troop_weights.xml is a flat per-troop-id lookup read by TroopWeightXmlLoader; TroopWeightService.GetTroopWeight returns 1.0 for an unlisted id. 41 rows moved from 2.0 to 3.0: every listed troop whose troops_*.xml level is 41 or 46. The file now has 52 rows at 2.0, 51 at 3.0, one at 4.0 (taom_spider_creature), one at 10.0 (harad_elephant_rider). The band comments were rewritten (the file used to argue that mounted branches never escalate with tier; that argument is now false and was removed). ComputeSizePenalty subtracts weighted minus raw from the party size limit, so an all-L41+ party fields base/3 raw troops instead of base/2.

TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid, use "dolguldur". Mordor's clans are clan_empire_south_N and its kingdom is empire_s.

READ FIRST
- docs/features/black-numenorean.md (sections "The Two Houses", "min_value is half the mechanic", "Expect smaller armies", "Standalone elite line", "Party-template stacks must be followed by a re-normalise", and the numbered hazards near the end including "A clan-heraldry regeneration would delete this feature")
- docs/features/lord-party-templates.md (Patch88, why Sauron has his own template)
- docs/features/troop-weight-system.md (sections "Count -> limit rework", "Usage frame", "Current Weight Tiers", "The level rule", the open all-heavy-culture question)
- docs/reference/party-template-sizing.md (the fill formula, "The 2026-09-04 floor bug")
- docs/features/culture-playability-wiring.md "Party-template binding contract"
- docs/reviews/rca-black-numenorean-confinement-2026-09-13.md (the deep-review RCA for this changeset; try to refute its findings and its "not a finding" notes)
- .claude/rules/troops.md "Party Template Types" and the max_value paragraph

FILES IN SCOPE (use git diff -- <path> for the tracked ones; the new files are untracked)
Data:
- Main/_Module/ModuleData/taom_partyTemplates.xml (183 deletions, 120 max_value edits; has a UTF-8 BOM and CRLF, tab indented)
- Main/_Module/ModuleData/TroopWeights/troop_weights.xml (41 value flips, comment rewrites)
C#:
- Main/Features/TaomSettings.cs (ONE line, the EnableTroopWeight HintText)
- TAOM.Tests/Core/BlackNumenoreanPartyTemplateTests.cs (new)
- TAOM.Tests/Features/TroopWeight/TroopWeightLevelBandTests.cs (new)
Tools:
- tools/wire_black_numenorean_troops.py
- tools/generate_clan_heraldry.py (upsert_party_template)
- tools/tests/test_generate_clan_heraldry.py (new)
Docs (check claims against data, not prose style):
- docs/features/black-numenorean.md, docs/features/lord-party-templates.md, docs/features/troop-weight-system.md, docs/modding/balance-levers.md, docs/modding/configs-balance.md, tools/README.md (the wire_black_numenorean_troops.py and generate_clan_heraldry.py rows), docs/reviews/lessons/gamemodels-services.md (the re-count paragraph near line 420), docs/reviews/lessons/build-tooling-workflow.md (last entry), CHANGELOG.md (the two entries under ## 2026-09-13 only)
Unchanged code the data flows through (read, do not edit):
- Main/Features/TroopWeight/TroopWeightService.cs, TroopWeightXmlLoader.cs, TroopWeightDisplay.cs, Hooks/TroopWeightDisplayHook.cs, Hooks/PartyUpgraderUpgradeReadyTroopsHook.cs, TroopShedPlanning.cs
- Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs
- Main/Features/LordPartyTemplates/ (Patch88 and its service)
- Main/_Module/ModuleData/taom_spcultures.xml (mordor block, the party template bindings), Main/_Module/ModuleData/spcultures.xslt, Main/_Module/ModuleData/spclans.xslt (clan_empire_south_1 and _9 bindings), Main/_Module/ModuleData/characters/clans.xml (clan_empire_south_10..15)
- Main/_Module/ModuleData/clan_heraldry/mordor.json and the other 20 specs
- tools/rebalance_party_template_maxes.py

KNOWN SUSPECTS (CONFIRM or DISPUTE each with evidence)

S1. The removal changed spawn COMPOSITION beyond the BN stacks. For the 14 lord templates the rescale multiplies every remaining stack's spread by one factor, so the orc/uruk/warg proportions are preserved and only the ceiling is restored. Hypothesis to test: rounding drift is absorbed by the widest stacks, so a thin stack (max 4, the uruk stacks in the culture default and several clan templates) could have gained or lost a body in a way that changes the tier mix. Recompute per template the old and new max sums by tier band (level < 21, 21..36, >= 41) and report any template whose band shares moved by more than 2 percentage points. If none, say so with the numbers.

S2. The engine may still hand a non-house Mordor lord Black Numenoreans through a path that is not his clan template. Enumerate every engine path that adds troops to an AI lord party in 1.4.8 (HeroSpawnCampaignBehavior.SpawnLordParty initial fill and its new-game bonus fill over the DEFAULT template; the daily recruitment from settlements and notables via RecruitmentCampaignBehavior, which draws from the culture's basic/elite volunteer trees; PartyUpgrader upgrades; prisoner recruitment; garrison transfers; army merges; the vassal reward, which is player-only) and state for each whether a mordor_num_ troop can enter a non-house lord's roster. The upgrade tree question matters: mordor_num_initiate upgrades only within the line, and nothing outside the line upgrades INTO it. Verify by grepping upgrade_targets in troops_mordor.xml.

S3. The patrol template lost its min==max 1/1 BN stack, so the level-3 Mordor patrol sums to 17 instead of 18. Confirm from DefaultPartySizeLimitModel.GetInitialPartySizeRatioForMobileParty that patrol parties fill at ratio 1 (every stack to its max) and that CalculatePatrolPartySizeLimit is a separate recruitment cap, so the only effect is one fewer body. Also confirm that no other culture binds patrol_party_mordor_template_level_3 (Khand binds the Rhun patrol per spcultures.xslt).

S4. The weight rule is level-based but the weight file is per-id, and the level attribute lives in another session's staged troop rebalance. Confirm that the staged troops_*.xml diff (git diff --cached -- Main/_Module/ModuleData/troops/) touches no level= attribute, so TroopWeightLevelBandTests reads the same levels whether or not that other work lands. If it does touch a level, name the troop and whether it crosses the 41 boundary.

S5. Raising 51 troops to 3.0 could push ComputeSizePenalty into its clamp for real parties. The clamp is min(penalty, baseLimit - 1) with a baseLimit < 2 early-out. A Rivendell lord's base limit is vanilla's GetPartyMemberSizeLimit (about 40 to 203 depending on clan tier, renown perks and TAOM feats; Rivendell has no party-size feat). Reason about an all-L41+ Rivendell party of 60 raw at 3.0 each: weighted 180, penalty 120, clamped to baseLimit-1. State what the player-facing limit reads (the display frame shows weighted-used over true base since 2026-09-06) and whether any display surface can show a negative or zero limit. Read TroopWeightDisplay.cs and TroopWeightDisplayHook.cs for the arithmetic, and SubtractResultFramePenalty for the factor division.

S6. SubtractResultFramePenalty divides the penalty by 1 + SumOfFactors so factor-boosted cultures pay exactly the penalty in slots. With larger penalties, does the division plus ExplainedNumber's rounding ever leave a party 1 slot over or under? Read ExplainedNumber.Add in the installed TaleWorlds.Core (pwsh tools/taom-src.ps1 path TaleWorlds.Core.ExplainedNumber) and state whether the base and factor accumulate in float and whether the final ResultNumber cast to int in PartyBase.PartySizeLimit floors or rounds.

S7. The generate_clan_heraldry.py guard now compares the spec's max sum against the live template's. A spec that is LEGITIMATELY intended to lower a template (a deliberate nerf written into the JSON) will be refused. Is there any documented workflow that lowers via the spec? Read the tool's docstring and the docs that reference it (grep -rn generate_clan_heraldry docs/ tools/README.md). If lowering via the spec is a supported path, this guard blocks it and needs a flag; if the documented path is "regenerate the spec from the live file, then edit", the guard is correct. Give a verdict.

S8. The new test BlackNumenoreanPartyTemplateTests allows exactly four templates by id. If a future template is added with a legitimate BN stack (say a Mordor-culture mercenary band), the test fails and the author extends the allow list; that is intended. But check the opposite hole: does any template OUTSIDE taom_partyTemplates.xml (an XSLT-produced template, or a template in TAOM_Map / LOTRLOME_Armory ModuleData, or vanilla SandBoxCore spSpecialCharacters party templates that TAOM's spcultures.xslt still binds for Culture.mordor) reference mordor_num_ troops? grep the live modules under E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\{TAOM_Map,LOTRLOME_Armory,SandBoxCore,SandBox}\ModuleData for mordor_num_.

S9. The test TroopWeightLevelBandTests skips ids that resolve to no troop but asserts every unresolved id is a named mount package. taom_spider_creature is in troops? Actually it is not in troops_*.xml. Check whether it is defined anywhere (characters/*.xml, a creature file) with a level, and whether the test's MountPackages exemption would silently hide a future renamed troop that happened to be named like a mount package (it would not, the set is explicit). Also check: does any troops_*.xml troop lack a level attribute, so it is invisible to both tests? List any.

REQUIRED SECTIONS

1. VANILLA CODE. Decompile and paste as code blocks (from the installed DLLs via pwsh tools/taom-src.ps1 path <Type>, or from E:\Decompiled_Bannerlord\_categories_v1.4.8\ if the signature matches): DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty and GetInitialPartySizeRatioForMobileParty; PartyTemplateObject.Deserialize; HeroSpawnCampaignBehavior.SpawnLordParty (the bonus fill loop); Clan.DefaultPartyTemplate; PartyBase.PartySizeLimit; ExplainedNumber.Add and ResultNumber.

2. TEMPLATE ARITHMETIC. For each of the 15 edited templates print old and new: stack count, min sum, max sum, and the per-tier-band max shares from S1. Confirm no stack has max < min and no stack went to max 0.

3. WEIGHT BAND. Produce the full table of the 105 live weight rows joined to level and culture. Confirm 41 changed rows are exactly the set at level 41 or 46 that were 2.0, that every 3.0 row is level 41+ (or one of the ten pre-existing capstones at 46/51), and that no 2.0 row is level 41+. Name any troop at level 41+ in any culture that has NO weight row at all (those pay 1.0; this is a pre-existing gap, report it as an observation with the list, not as a finding of this changeset).

4. CONFIG CROSS-REFERENCE. Every culture=, troop=, PartyTemplate. reference in the diff resolves (python tools/validate_moduledata.py is the repo tool; run it read-only and report the error count). Every clan binding named in this prompt actually points where the prompt says (spclans.xslt lines for clan_empire_south_1 and _9, characters/clans.xml for _10.._15, lord_party_templates.json for lord_1_17).

5. TESTS. Do the two new C# tests and the new Python test actually pin what the CHANGELOG says? Write down one mutation per test that the test would NOT catch (a blind spot), and say whether it matters.

6. FINDINGS OR OBSERVATIONS. For each finding: severity (P1 blocks ship, P2 should fix, P3 nit), file:line, the exact defect, a reproduction or the proving code, and the fix. Distinguish findings of THIS changeset from pre-existing defects you happened to see (list those separately as PRE-EXISTING with the same rigor). If you find nothing at a severity, say "no P1", "no P2" explicitly.

QUALITY GATES
- Every claim about engine behaviour cites a decompiled file and line.
- Every count you quote was produced by a command you ran; paste the command.
- Refute before you agree: for each Known Suspect say CONFIRMED or DISPUTED and why.
- Do not report a doc prose style issue unless it states a wrong number or a wrong id.
- Do not flag vanilla-matching behaviour as a bug.

PRIOR REVIEW LESSONS
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Review 95 on this model caught a min-floor rounding defect in rebalance_party_template_maxes.py that 8000 unit tests had passed.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex reported counts it had not computed.

Write your review as markdown to stdout. It is captured to docs/reviews/raw/codex-adversarial-bn-confinement-weight-band-2026-09-13.md.
