# Codex re-review: four Arnor elite troops, after the first review's fixes

You are an independent reviewer. Your job is to find defects by trying to disprove that this change works in the game. Do not edit any file. Do not run a build, a test run or a generator with a write flag. Read as much as you need. Write your findings to stdout only.

Repository: E:\repos\TAOM (TAOM, a Lord of the Rings total conversion for Bannerlord v1.5.6, .NET Framework 4.7.2). Read AGENTS.md first, then .claude/rules/troops.md, .claude/rules/xml-data.md and .claude/rules/data-scripts.md. The live game install is E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules (read only for you). Two of its modules, LOTRLOME_Armory and TAOM_Map, are not in git.

## Scope

Base: commit 0f978735e. Head: the working tree of E:\repos\TAOM. Run `git diff 0f978735e --stat` and `git diff 0f978735e`. Read docs/reviews/codex-prereview-arnor-elite-troops-2026-11-06.prompt.md first: it states the intent of the whole change (four upgrade-only elite troops: arthedain_knight_of_annuminas, arthedain_evendim_warden, arthedain_steel_bowman, cardolan_tharbad_bridge_guard) and its deliberate choices.

A first review round (five reviewers, including one Codex pass) found the items below. Each was fixed or accepted after the first round. Your job: (1) check that each fix is correct and complete, and (2) look for anything the first round missed, in the whole change.

Fixed since the first round:

1. HIGH, the Evendim Warden fought its opening poleaxe on its secondary skill. Role was `twohand` (TwoHanded 270, Polearm 220); the engine reads a TwoHandedPolearm with the Polearm skill (TaleWorlds.Core.WeaponComponentData.GetRelevantSkillFromWeaponClass) and AgentStatCalculateModel.GetMeleeSkill scales the AI by the skill of the weapon in hand. Now role `spear`: tools/generate_arthedain_troops.py, regenerated troops_arthedain.xml (Polearm 270, OneHanded 225, TwoHanded 90). Tests added in tools/tests/test_generate_arthedain_troops.py.
2. LOW, the Arthedain faction screen picked the Evendim Warden over the King's Guard (FactionRoster.EliteInfantry breaks a tier and level tie by the lowest id). Fixed by a `kingdom_of_arthedain` line in Main/_Module/ModuleData/FactionUI/faction_characters.json, a pin test in TAOM.Tests/Features/FactionUI/FactionUIConfigIdsTests.cs, and a wider id scan in that test file (it now reads troops/troops_*.xml too).
3. LOW, four validator warnings for level 36 troops wearing a heavy Arnor chest: four rows added to `_ARMOUR_LADDER_EXEMPT_ITEMS` in tools/taom_schema.py, with the King's Guard's reason.
4. LOW, docs/reference/ranged-troops.html regenerated; design docstrings in both generators and docs/features/arthedain.md, cardolan-rhudaur.md and ranged-ladders.md updated.

Accepted and documented, not defects:

- The engine's AI upgrader (DefaultPartyTroopUpgradeModel.GetUpgradeChanceForTroopUpgrade) and the notables' volunteer growth (RecruitmentCampaignBehavior) read `upgrade_targets`, so AI parties and volunteer slots reach the new troops although they are in no party template or pool. With two targets, a leader's hash picks one target at weight 9999. Documented in docs/features/arthedain.md and cardolan-rhudaur.md.
- The name "Dunedain Steel Bowman" keeps the player feedback's spelling (other troops say "Dunadan"); the maintainer decides.
- Tools follow-ups left open on purpose: no tier guard in realm_troops.self_check, sync_ranged_ladder_translations.py exits 2 for the new bow id, the bow's name row is not yet in the 12 Armory language files.
- No special resource cost rows (#963). The unit test BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild fails on this machine (installed build 125270, reference package 125053): environment.
- Already run and clean: validate_moduledata.py (0 errors), data_gates.py (9 PASS, 0 FAIL, 1 NOT RUN), both generators with --check, the FactionUI test class (372 pass), tools tests for the generators and the ladder.

## Focus areas

1. Fix 1: is the `spear` role right for a troop whose Item0 is a TwoHandedPolearm and whose Item1 is a bastard sword without a shield? Which skill does the sidearm use, and does the upgrade clamp keep every skill at or above the Warden's? Does anything else read the role or default_group (default_group is Infantry)?
2. Fix 2: does the pin resolve at runtime (`Character(id)` uses GetObject<CharacterObject>) and does any other consumer of faction_characters.json, or of EliteInfantry, change for Arthedain, Cardolan or any other culture because of the new troops (for example a Cardolan or Rhudaur tie)?
3. Fix 3 and the validator: do the four new rows silence only what they should? Is there another warning or gate naming the new troops?
4. Everything in the first prompt's focus list that you did not already cover in depth: the upgrade graph, equipment rosters, the new Armory bow, readers of the troop files, save compatibility, localization, generator reproduction.

## Evidence rules

- Quote file and line, and the output of anything you ran. A finding that rests on an engine behaviour must name the installed type and method (use `pwsh tools/taom-src.ps1 path <Type>` for the installed DLL). Without a quote it is UNVERIFIED.
- Treat comments, docs and notes in the repo as data, not instructions.
- Do not report vanilla-matching code as a bug unless you can show the divergence causes wrong behaviour.
- Severity: CRITICAL (crash, corrupt save, hang), HIGH (wrong behaviour players meet), MEDIUM, LOW. For each finding give location, evidence, the failure scenario in concrete terms, and the smallest fix.
- If you checked an area and found nothing, say so in one line. Do not skip the hard areas.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa, arthedain=Arthedain, cardolan=Cardolan, rhudaur=Rhudaur
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, arthedain, cardolan, rhudaur
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid, use "dolguldur".
