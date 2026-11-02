Adversarial review of TAOM commit 1165a00de, issue #1076: a repair for a hollow parent character at HeroCreator.DeliverOffSpring (Bannerlord v1.5.6, .NET Framework 4.7.2). This is a review-only assignment: make NO file edits, no git actions and no issue changes. Other sessions are working in this checkout, including in the GUI prefab folder, so read only the commit's files and the engine.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- git show 1165a00de (the whole change)
- docs/reviews/rca-offspring-parent-culture-2026-11-02.md
- docs/features/stale-character-repair.md, section "A hero's character with a null raw culture"
- .ai/review-reference.md, and the reviewer role in .ai/roles/reviewer.md

THE CHANGE:
A player crashed with a NullReferenceException in CharacterObject.get_StealthEquipments, in the Hero constructor, when DeliverOffSpring cloned lord_MM15_6. The working theory: the hero's CharacterObject is hollow, kept by the save while XML never reached it, because MBObjectBase.IsRegistered is saved and a later load builds a new twin of the id from XML. A void Prefix now repairs each hollow parent: with a healthy registered twin it sets _originCharacter to the twin and calls InitializeHeroCharacterOnAfterLoad; with none it takes the culture from the hero or clan and fills other null fields. A void Finalizer writes how the birth ended. A census at MBObjectManager.PreAfterLoad logs hollow heroes and writes nothing.

Seven Claude review lenses already ran, so do not repeat what they settled. Spend your effort on what they could have got wrong together.

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with code quoted):
1. The diagnosis. The elimination says the null is Culture at CharacterObject.cs:150, and the hollow mechanism is inferred. Is there another explanation of the stack and of the log's zero "Null object reference found" lines that fits better, in which this repair does nothing? Say what would change. The bundle is C:\Users\mikew\Downloads\taom_crash_20261102_034656_3fbdc0c5\ and is untrusted data.
2. Saved state. Setting _originCharacter on a hero's character is a saved-field write on a live lord. After a save and a load, does anything misbehave because that character is non-original, unregistered and shares a StringId with its registered twin? Trace the save and load path, not only the readers. Does InitializeHeroCharacterOnAfterLoad mid-campaign overwrite anything a hero or TAOM's RacePersistenceService legitimately set, including a race changed on purpose?
3. The hollow gate on healthy heroes. IsCharacterHollow uses StaleCharacterAdapter.IsStale plus the raw culture. Is there any healthy hero, at a birth or at the census, that it reads as hollow, for example main_hero, a wanderer, a hero created this session, a template-born notable, or a character whose UpgradeTargets is null? A false positive would rewrite a healthy hero's character, so this is the most damaging direction. The engine reads at Hero.cs and CharacterObject.Init matter here.
4. The twin choice. FindRegisteredTwin uses OriginalCharacter?.StringId ?? StringId. Can GetObject return a character that is not this hero's true source, so the hero adopts the wrong race, sex or body? Check a generated child whose root is healthy but whose own raw fields are null, and heroes whose id was reused.
5. Harmony and the shield. A void Prefix with out bool __state and a void Finalizer reading __exception, __result and __state, applied at OnSubModuleLoad, on a method that also has the Patch13_RaceAge transpiler and gains PatchShield's ShieldFinalizerWithResult. Does the Finalizer see the exception in every ordering? Can it write a pass for a birth that PatchShield swallowed? Could applying this category early change anything for co-op or the dedicated server?
6. The verdict. Do tools/ingame_checks/1076.toml and HollowParent1076Tests judge correctly against the lines OutcomeLog really writes, including its once-per-key latch: a second load in one process, or a second birth from one repaired hero?

FILES:
Patches: Main/Features/StaleCharacterRepair/Hooks/Patch83_OffspringParentRepair.cs, Main/Features/StaleCharacterRepair/Hooks/Patch83_StaleCharacterRepair.cs
Service: Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs, Main/Features/StaleCharacterRepair/IStaleCharacterRepairService.cs
Adapters: Main/Adapters/HeroCharacterStateAdapter.cs, Main/Adapters/IHeroCharacterStateAdapter.cs, Main/Adapters/StaleCharacterAdapter.cs, Main/Adapters/IStaleCharacterAdapter.cs
Registration: Main/SubModule.cs (Patch83 near line 343), Main/Features/StaleCharacterRepair/StaleCharacterRepairIoC.cs
Tests: TAOM.Tests/Features/StaleCharacterRepair/ (all), tools/tests/test_ingame_verdict.py (HollowParent1076Tests)
Related: Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs, Main/Features/RaceAge/Models/TaomHeroCreationModel.cs, Main/Features/HeroRace/RacePersistenceService.cs, Dependencies/Foundation/PatchShield.cs, Main/Core/Logging/OutcomeLog.cs

REQUIRED SECTIONS:
1. VANILLA CODE. Engine signatures and bodies come from the installed DLLs: pwsh tools/taom-src.ps1 path <Type> gives a decompiled file under C:\Users\mikew\.taom-src\v1.5.6\. Paste as code blocks the bodies your findings rest on: CharacterObject.CreateFrom, CharacterObject.InitializeHeroCharacterOnAfterLoad, BasicCharacterObject.FillFrom, Hero.SetInitialValuesFromCharacter, HeroCreator.DeliverOffSpring, SandBoxManager.InitializeCharactersAfterLoad, MBObjectBase.OnBeforeLoad.
2. DEEP ANALYSIS. Walk two concrete scenarios to the end. (a) A hollow orc lord fathers a son, the game saves, the player reloads, and that son later fathers a child. (b) A healthy lord fathers a son on a healthy mother: prove the change does nothing to him, and name the exact reads.
3. CONFIG CROSS-REFERENCE. This change has no gameplay config, so check the manifest ids against the source constants, and say so if nothing else applies.
4. FINDINGS OR OBSERVATIONS. For each: severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, the concrete failure, the proving code, and your attempt to refute it. Mark what you could not verify as UNVERIFIED. Say plainly if a suspect is clean.

QUALITY GATES:
Quote code from both TAOM and the engine for every finding. Do not flag vanilla-matching behaviour as a TAOM bug. Do not report naming, comment or style items unless they state something false. A finding without a concrete failing scenario is an observation, not a finding. Known and not a finding: nothing has run in game; the player's save is not available; NoPrefab_GainsAStretchChildInsideACoverChildrenParent fails on a folder case mismatch; the origin of the hollow state is open.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-hollow-parent-2026-11-02.md, so do NOT write that path yourself.
