# Input pack: WP 126 race-and-hero-fixes

Title (draft): Hero race fixes: dead heroes, dwarf portrait offset, Nazgul race
Wave: 3   Lane: D   Draft priority: P1   Groups: 3   Port commits: 5
Depends on (WP ids): 105

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `issue-644-nazgul-race`  P2  track=data-integrity  commits=2  sim: clean=0 conflict=2
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/HeroRace/RacePersistenceService.cs, Main/Features/SignatureStrikes/Hooks/StrikeSoundPlayer.cs, Main/Features/SignatureStrikes/SignatureStrikesConfigProvider.cs, TAOM.Tests/Core/CultureRaceConsistencyTests.cs, TAOM.Tests/Features/CombatMechanics/ShippedCombatMechanicsConfigTests.cs, TAOM.Tests/Features/SignatureStrikes/SignatureStrikesConfigProviderTests.cs
- `9804f67b9` 2026-09-23 PORT-PARTIAL P2 pick=conflict :: feat(nazgul): v2.0.30 - the Nine become race nazghul (#644)
  - why: Mostly content (the Nine made race nazghul), but it adds a test gate that every character race in ModuleData XML and XSLT is registered by skins.xml, because FaceGen.GetRaceOrDefault throws on an unknown name (also true on v1.4.8, TaleWorlds.MountAndBlade/FaceGen.cs:115-118).
  - port notes: Take CultureRaceConsistencyTests.EveryCharacterRaceIsARealRegisteredRace and the RegisteredRaces helper; its skins.xml paths are hard-coded to E:\Steam, so also resolve BANNERLORD_GAME_DIR or it goes Inconclusive on the laptop. Leave the race change, lords.xslt/lords.xml edits, config race lists, the RacePersistenceService skip and NazgulRaceDataTests unless the maintainer takes the content. The lord_1_48_1/2/3 double definition it repairs also exists on 1.4.x (characters/lords.xml:24935, :24986, :25037 with race uruk, plus lords.xslt templates from :6106); deleting the rows alone would turn the three human.
  - conflicts: TAOM.Tests/Features/CombatMechanics/ShippedCombatMechanicsConfigTests.cs
  - issues: 644, 648
- `437d59111` 2026-09-23 PORT-PARTIAL P2 pick=conflict :: fix(nazgul): v2.0.30 - Codex review follow-ups for #644 and #645
  - why: Codex follow-ups that harden the race-registration gate to parse each stylesheet and read every race verbatim; the rest is SignatureStrikes and a #644 comment.
  - port notes: Take the CultureRaceConsistencyTests change (RacesEmittedByXslt parses stylesheets and fails on a race computed at transform time) and the .claude/rules/xslt.md paragraph. Leave the SignatureStrikes provider and sound hunks (1.5.x-only) and the RacePersistenceService comment (belongs to the #644 race change). REVIEW-LOG, codex-track-record and lessons are bookkeeping.
  - prereqs: 9804f67b9
  - conflicts: Main/Features/HeroRace/RacePersistenceService.cs, Main/Features/SignatureStrikes/Hooks/StrikeSoundPlayer.cs, Main/Features/SignatureStrikes/SignatureStrikesConfigProvider.cs, TAOM.Tests/Core/CultureRaceConsistencyTests.cs, TAOM.Tests/Features/SignatureStrikes/SignatureStrikesConfigProviderTests.cs
  - issues: 644, 645

### group `herorace-dwarf-portrait-offset`  P2  track=shared-feature-fix  commits=1  sim: clean=1 conflict=0
- `6c26f003a` 2026-10-11 PORT P2 pick=clean :: fix(heroRace): v2.0.34 - dwarf portraits no longer sit too low
  - why: Corrects the dwarf 2D portrait offset in CharacterImagePatch.json; origin/bannerlord-1.4.5 carries the same pre-fix value (Vertical 0.15, Zoom -0.1) and the same HeroRace RacePositionStore reads it.
  - port notes: One-line JSON change, clean pick. The values were tuned by eye on 1.5.x; confirm framing in game on v1.4.8 before release.

### group `issue-1011-hero-race-dead-heroes`  P1  track=shared-feature-fix  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/HeroRace/RacePersistenceService.cs, TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs
- `f981629cd` 2026-10-26 PORT P1 pick=conflict :: fix(hero-race): v2.0.35 - dead heroes keep their race across a load
  - why: Dead or disabled heroes lose their saved race on load because HeroRosterAdapter reads Hero.AllAliveHeroes; origin/bannerlord-1.4.5 has the same adapter, and v1.4.8 SandBoxManager.InitializeCharactersAfterLoad (SandBoxManager.cs:384-403) re-initializes dead and disabled hero characters too.
  - port notes: Take the switch to Hero.FindAll/FindFirst (v1.4.8 Hero.cs:2130-2145, which scan Campaign.Characters), the GetAllHeroRaces rename, the two service call sites and the tests. RacePersistenceService.cs conflicts lightly because the 1.5.x parent has about 21 more lines. The saved _taom_heroRaceMap dictionary (RacePersistenceService.cs:179) grows from alive heroes to every hero, so given the save-time evidence, time one long-campaign save before and after. Docs and lessons are bookkeeping.
  - conflicts: Main/Features/HeroRace/RacePersistenceService.cs, TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs
  - issues: 1011
- `0f7902684` 2026-10-26 PORT P3 pick=conflict :: docs(hero-race): v2.0.35 - co-op race restore checked, no change
  - why: Corrects the comment and hero-race doc claim that a co-op save transfer skips OnBeforeSaveEvent; the same stale comment is on origin/bannerlord-1.4.5 at RacePersistenceBehavior.cs:26.
  - port notes: Comment, test-comment and doc edits only. The co-op module behavior it cites (SaveInterface.cs:109) was read for the module build used on 1.5.x; confirm it against the co-op module version used with 1.4.8 before stating it on that line.
  - issues: 1022

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 3, batch 15, batch 28
