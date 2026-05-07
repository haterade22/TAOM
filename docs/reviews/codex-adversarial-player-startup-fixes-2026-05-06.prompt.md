PHASE 3 ADVERSARIAL SELF-REVIEW: Review the fixes applied this session (2026-05-06) in response to your prior review of issue #110 (player startup gold + CC equipment persistence port from LOTRAOM StartingEquipmentGold). You ARE the prior reviewer. Your job now is to find what your first review missed, what the fixes themselves broke, and what is still inconsistent across code + docs + memory.

The full session is uncommitted. Use `git diff HEAD` and `git ls-files --others --exclude-standard` to see everything.

# TAOM ID CHEATSHEET (for cross-reference)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, shaghana, abanissa

Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale

NOTE: shaghana and abanissa are full INDEPENDENT KINGDOMS in the Harad region (registered in taom_spkingdoms.xml with their own ruler titles Taskral / Châjaphân, banner keys, settlements). They are NOT Aserai sub-cultures. They have NPC clans + lords (Shaghana 9 lords, Abanissa 8 lords).

# READ FIRST

- docs/features/startup-resources.md (extended with player gold + equipment sections this session)
- docs/features/character-creation.md (OnCharacterCreationFinalize description updated)
- docs/features/kingdom-creation.md (Shaghana/Abanissa rows expanded)
- docs/reviews/rca-player-startup-2026-05-06.md (root cause analysis of all 7 bugs found this session)
- Main/_Module/ModuleData/charactercreation/cultures.json (CC-selectable cultures - source of truth)
- Main/_Module/ModuleData/charactercreation/youth_menu.json (youth options per culture)
- Main/_Module/ModuleData/startup_resources/startup_resources_config.xml (per-culture gold + influence + playerGold)
- Main/_Module/ModuleData/taom_spkingdoms.xml (kingdom registrations - confirms shaghana/abanissa as kingdoms)
- Main/_Module/ModuleData/characters/lords.xml (NPC lord registrations - confirms 9 shaghana + 8 abanissa)

# Known Suspects -- CONFIRM or DISPUTE each

These are the issues most likely to still be wrong. For each: state CONFIRMED or DISPUTED with a specific file:line reference.

## Suspect 1: DeadCivilianEquipment guard correctness

The fix in Main/Adapters/PlayerEquipmentAdapter.cs:30-37 introduces:
```
var deadBattle = Campaign.Current?.DeadBattleEquipment;
var deadCivilian = Campaign.Current?.DeadCivilianEquipment;
if (battle != null && hero.BattleEquipment != null && hero.BattleEquipment != deadBattle)
    hero.BattleEquipment.FillFrom(battle);
if (civilian != null && hero.CivilianEquipment != null && hero.CivilianEquipment != deadCivilian)
    hero.CivilianEquipment.FillFrom(civilian);
```

Re-verify via ilspycmd against installed v1.3.15 DLL at E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll:
- Confirm `Campaign.DeadCivilianEquipment` is a separate property/singleton from `DeadBattleEquipment`.
- Confirm `Hero.CivilianEquipment` getter falls through to `DeadCivilianEquipment` (not `DeadBattleEquipment`).
- Reference equality check is correct: is `Equipment` a reference type and is `DeadCivilianEquipment` populated as a singleton initialized once?

## Suspect 2: Player flow for shaghana/abanissa cultures (NO youth options)

These two cultures are CC-selectable per cultures.json BUT have ZERO entries in youth_menu.json (grep confirms: only 16 distinct culture_ids appear in youth_menu, missing shaghana and abanissa).

Trace the end-to-end CC flow for a player picking shaghana:
1. CC culture step selects shaghana. SetSelectedCulture sets SelectedTitleType = DefaultSelectedTitleType (whatever vanilla default is - check what value).
2. Youth menu step has NO TAOM options for shaghana (no string_id starting with taom_youth_shaghana_*). What does the CC menu actually show? Vanilla aserai options? Empty list? Crash?
3. At finalize, GrantPlayerStartupResources is called with cultureId="shaghana" and titleType=whatever was selected. PlayerStartupGoldService grants 4000 gold (correct - row exists in XML). PlayerEquipmentService tries to apply roster `player_char_creation_shaghana_{titleType}_{m|f}` - does any such roster exist? Grep equipment XMLs.
4. If no roster exists, PlayerEquipmentService logs RosterNotFound warning and skips. Player walks in with vanilla default equipment + 4000 gold. Is this the intended behavior, or is it a gap that should be filled?

State CONFIRMED or DISPUTED: is the shaghana/abanissa player flow actually working end-to-end, or are there gaps similar to sturgia_retainer?

## Suspect 3: Are OTHER cultures still missing from startup_resources_config.xml?

The session added empire (Dunland), shaghana, abanissa. The data-flow agent earlier found these by cross-referencing cultures.json. Re-verify:
- Read cultures.json. Extract all culture_id values.
- Read startup_resources_config.xml. Extract all id values.
- Compute set difference. Is the diff zero?
- Also check: are there any culture IDs in the XML that DON'T exist in cultures.json? (Dead config rows)

## Suspect 4: shaghana/abanissa NPC tier appropriateness

I set shaghana/abanissa to gold=50000, influence=100, playerGold=4000 (matching aserai/Harad tier). But:
- Aserai: 28 NPC lords -> ~1786 gold per lord on average
- Shaghana: 9 NPC lords -> ~5556 gold per lord on average  
- Abanissa: 8 NPC lords -> ~6250 gold per lord on average

Wait - that's wrong. Read StartupGoldService.cs - is `gold="50000"` a per-culture pool or a per-lord amount? If per-lord: shaghana lords get 50000 each, total 450000. If per-culture pool: 50000 split across all lords. Verify which interpretation is correct, then assess whether 50000 is the right tuning value for these smaller-roster kingdoms.

## Suspect 5: Doc consistency -- residual "Aserai-region" or "sub-kingdom" wording

Grep all files for "Aserai-region", "sub-kingdom", "no NPC clans" in the context of shaghana/abanissa. Any residual?

Files most at risk:
- CHANGELOG.md (was rewritten this session - verify consistent)
- docs/features/kingdom-creation.md (table row was rewritten)
- docs/features/startup-resources.md (table row was rewritten)
- docs/features/character-creation.md
- docs/reviews/rca-player-startup-2026-05-06.md (the RCA itself - does it accurately describe what got fixed?)
- Memory files at C:\Users\mikew\.claude\projects\c--Users-mikew-source-repos-TAOM\memory\

## Suspect 6: New bugs introduced by the fixes

- The CareerMenuService.cs:227 fix routes through PlayerEquipmentRosterIds.Build. But PlayerEquipmentRosterIds is `internal static` - is CareerMenuService in the same assembly + namespace? Confirm the call compiles AND produces the same string.
- The DeadCivilianEquipment guard introduces TWO local variables. If `Campaign.Current` is null at CC finalize (before campaign initialized?), both `deadBattle` and `deadCivilian` are null. Then `hero.BattleEquipment != deadBattle` (= `hero.BattleEquipment != null`) which is true (since hero.BattleEquipment is non-null). So FillFrom proceeds. Is that the right fail-open behavior, or should we fail-closed (skip if Campaign.Current is null)?
- ParsePlayerGold range [0, 10_000_000]: do any seed values in the XML hit this exact upper bound? Should the bound be tighter (e.g., 1M) given that NPC lord gold = 50000 and player gold = 10000 max in seeds?

## Suspect 7: RCA file claims vs code reality

Read docs/reviews/rca-player-startup-2026-05-06.md and verify each claim:
- "Bug B (sturgia retainer) - Changed taom_youth_sturgia_1 title_type to guard" - confirm in youth_menu.json line 821.
- "Bug E - PlayerEquipmentAdapter civilian guard fix" - confirm DeadCivilianEquipment singleton check is correct.
- "Bug G - shaghana/abanissa misclassified" - confirm XML now has gold=50000 influence=100 (not 0/0).
- The RCA claims 7 bugs found in 1 session across 3 systemic root cause classes. Does the bug list match what was actually fixed?

# REQUIRED SECTIONS in your review output

## VANILLA CODE (decompile + paste)

For Suspect 1, paste the actual ilspycmd output for:
- `Hero.BattleEquipment` and `Hero.CivilianEquipment` getters (just the `=>` expressions)  
- `Campaign.DeadBattleEquipment` and `Campaign.DeadCivilianEquipment` declarations + initialization

## CONFIG CROSS-REFERENCE

Build a 3-column table: culture_id | in cultures.json? | in startup_resources_config.xml?

Flag any row where the answer differs.

## TRACE: shaghana player end-to-end flow

Walk through what happens when a player picks shaghana at CC, in detail. What do they see at the youth-menu step? What is `SelectedTitleType` at finalize? What gold do they get? What equipment?

## FINDINGS

For each finding:
[SEVERITY] file:line -- finding -- remediation

Severities:
- CRITICAL: ships and breaks the game (crash, save corruption, silent data loss)
- HIGH: ships and breaks user-visible behavior (wrong equipment, wrong gold, silent no-op for selectable culture)
- MEDIUM: code quality issue or doc inconsistency that surfaces under specific conditions
- LOW: style, comment, defensive suggestion

End with: CRITICAL: N | HIGH: N | MEDIUM: N | LOW: N | VERDICT: CLEAN / ISSUES FOUND

# QUALITY GATES

- DO NOT skip the vanilla decompilation -- it caught the original P1 bug. Re-decompile.
- DO NOT trust the prior review's verdict -- this is a SECOND PASS adversarial review of the FIXES.
- Cross-reference cultures.json against startup_resources_config.xml at the row level.
- Read the RCA file and confirm each claim against actual code state.
- If you find ANY new HIGH or CRITICAL bug, that proves Phase 3 was needed -- the user explicitly asked for self-review of fixes because Phase 2 caught Phase 1's bugs and the pattern continues.

# Prior review lessons (apply this session)

SUCCESSES from this session's prior Codex review:
- Re-decompiling Hero.CivilianEquipment caught the P1 bug Claude missed (DeadBattleEquipment vs DeadCivilianEquipment)
- Cross-referencing cultures.json against startup_resources_config.xml caught shaghana/abanissa missing
- Pushing back on Claude's "may be intentional" hedge was correct -- they were genuine bugs

FAILURES to avoid:
- Don't accept "may be intentional" or "probably correct" hedges -- verify
- Don't trust agent paraphrase of decompilation output -- re-run ilspycmd
- Don't classify IDs from a single source -- grep across kingdom/clan/lord XML

DO NOT skip the per-suspect CONFIRMED/DISPUTED verdict -- skipping suspects is the failure mode the prior review was designed to avoid.

Output your review to: docs/reviews/codex-adversarial-player-startup-fixes-2026-05-06.md
