# Codex pre-review: Bannerlord v1.5.7 engine bump

REVIEW ONLY. Do not edit, create, delete or format any file in this checkout. Do not run git write commands. Do not run ./build.ps1. Do not run `dotnet build` or `dotnet test`: another process uses this worktree's build output. Read, search, decompile and run read-only commands only. Return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-prereview-engine-bump-v1-5-7-2026-11-10.md: do NOT write that path yourself.

## Feature

TAOM (a Lord of the Rings total conversion for Bannerlord, .NET Framework 4.7.2) moves from Bannerlord v1.5.6.125270 to v1.5.7.125802 (issue #1112). The change is uncommitted in this worktree (branch chore/engine-bump-v1.5.7, base 3b805cf38). Read `git diff` and the untracked files listed below.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- AGENTS.md and .ai/review-reference.md (rules, ADRs, severity and evidence format).
- gh issue view 1112 (the intent).
- docs/migration/v1.5.7-impact.md and docs/migration/v1.5.7-diff-ranked.md (the bump record; check its claims, do not trust them).
- Engine code: the v1.5.7 decompile is in E:\Decompiled_Bannerlord\_shipping_build and _modules_build; the v1.5.6.125270 decompile is in the same folders with the suffix _v1.5.6.125270. The Modding Kit folder _editor_build is still v1.5.6: do not read it as v1.5.7. Ignore every non-vanilla module file in _modules_build (Coop__*, TAOM__*, TAOM.Dependencies__*, DualWield*, Diplomacy, AWOIAF*): they are not the engine. The installed DLLs under "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client" and Modules\*\bin are the authority if a decompile disagrees.
- The file-level engine diffs, already filtered: E:\Decompiled_Bannerlord\_diff_1.5.6_to_1.5.7\_shipping_build\*.real.diff and _modules_build\*.real.diff. They were produced by a scratch script that drops encoding-only lines, assert line numbers and build numbers. That filter is itself a suspect (item 9).

## Known suspects (try hardest to break these)

1. v1.5.7 changed `PartySizeLimitModel.GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false)` to `(PartyBase party, bool isCurrentlyAtSea, bool includeDescriptions = false)`. The change edits three TAOM sites: the override in Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs, the caller in Main/Features/KeepWardens/PatrolRosterHook.cs and two calls in Main/Features/DevConsole/Cheats/CareerPerkCheats.cs. Is each one semantically identical to its v1.5.6 behaviour, for a land party, a party at sea, a garrison, a patrol, a caravan, a non-mobile party? Vanilla's own callers pass `IsMobile && MobileParty.IsCurrentlyAtSea`; the TAOM callers pass `party.IsCurrentlyAtSea`. Is that the same value at every point the TAOM callers run (the patrol spawn call inside `FindAppropriateInitialRosterForMobileParty`, the cheat)?
2. Are there other TAOM references to that member that the compiler accepts but whose meaning changed, or that bind by name and arity: Main, Client, Dependencies, TAOM.Tests, reflection strings, `AccessTools` lookups, Harmony parameter injection by name (`includeDescriptions`), test doubles, source-text tests, the co-op interop patches, docs that state the signature as a current fact?
3. The services the override calls (`ICastlePatrolSizeHook`, `ICulturalFeatsService`, `IAiPartySizeService`, `ICareerPassiveService`, `IEyeOfSauronHook`, `ITroopWeightService`) do not receive the new flag. v1.5.7 applies the ship crew factor inside `base` when the flag is true. Does any TAOM step after `base` snapshot or divide out factors in a way that the at-sea flag now changes? Does v1.5.7's new caller `CampaignUIHelper.GetMainPartyHealthTooltip`, which passes `isCurrentlyAtSea: false`, change what TAOM's postfix on that tooltip writes or what the player reads?
4. The new gate TAOM.Tests/Features/CulturalFeats/PartyMemberSizeLimitCallShapeTests.cs. Can it pass on a wrong call (nested parentheses in an argument, arguments over several lines, a bool variable, `default`, a call through a delegate or interface, a literal inside a string or a comment), or fail on a correct one? Does it behave on LF and CRLF checkouts, and on an empty match?
5. Every other engine signature that v1.5.7 changed: `ICraftingCampaignBehavior.CreateCustomOrderForHero` and `CraftingCampaignBehavior.CreateTownOrder` gained a leading `Town`; `OnTroopScoreHit` on `PartyGroupTroopSupplier`, `MapEventSide` and `MapEventParty` gained a trailing `captain`; `CampaignUIHelper.GetTeleportingLeaderHero` moved to `TeleportationHelper`; `PlayerIdProvidedTypes` gained a member. Does TAOM reach any of them by a direct call, a string, reflection, a Harmony target, a UIExtenderEx mixin or a save definer? Are there further signature, enum, const or default-value changes in the v1.5.7 diff that the record does not list?
6. The change deletes TAOM's two GameText rows `str_last_seen_encyclopedia_entry_leading_army` and `str_last_seen_encyclopedia_entry_at_sea_leading_army` from Main/_Module/ModuleData/taom_module_strings.xml, with the 24 `taom_str_last_seen_*` rows in the 12 language files. Does Native v1.5.7 define both ids, with the variables `Helpers.HeroHelper.GetLastSeenText` sets for each? Vanilla's key is `{=*}`: what does the engine do with that key in a non-English game? Does anything in TAOM still name the ids or the keys (C#, XSLT, tests, the localization tooling, `tools/translation_cache`, `tools/translation_overrides`, allowlists, generated registries)? Did the deletion keep every other line, the line endings and the XML well-formedness of the 13 files?
7. Engine behaviour that changed under members TAOM uses without overriding them. Read the diffs and judge each against TAOM code:
   - `SiegeAssaultEventComponent.OnPlayerEncounterContinueBattle` now requires `PlayerSiege.PlayerSiegeEvent != null` for the branch that keeps the siege event. TAOM puts the player into siege battles from its enlistment and siege defence features (Main/Features/Enlistment, Main/Features/Siege, Main/Features/MapEventGuard, Main/Adapters/EncounterAdapter.cs). Does any TAOM flow depend on the old branch, or carry a workaround that now acts twice?
   - `IssuesCampaignBehavior.OnGameLoaded` now runs a one-time migration on a save older than v1.5.6.125291: it removes from every party roster, party prison roster and settlement prison roster each hero who is dead, or disabled and outside the player clan. Does any TAOM feature keep such a hero in a roster on purpose (Refuge, Player Switcher, Nazgul, wardens, enlistment, uncapturable heroes, hero commission), so that loading a v1.5.6 save on v1.5.7 breaks TAOM state?
   - `DisbandPartyCampaignBehavior.OnMobilePartyDestroyed` now sets `IsDisbanding`, and `EmptyClanPartiesCampaignBehavior.OnMobilePartyDestroyed` returns when `destroyerParty == null`. TAOM reads `IsDisbanding` and destroys parties itself (Refuge, Eye of Sauron, supply caravans, creature bands, raid hosts). Any changed outcome?
   - `PartyScreenLogic.TransferTroop` now removes XP from the source roster in both directions. TAOM builds its own `PartyScreenLogic` in three adapters and patches `AddCommand`, `UpgradeTroop` and `DoneLogic`. Any changed outcome?
   - `MobileParty.IsCurrentlyAtSea`'s setter and `FinishNavigationTransitionInternal` now call `MemberRoster.UpdateVersion()` where they called `UpdateVersionNo()`. TAOM has caches keyed on roster version and its own `PartySizeCacheInvalidator`. Any changed outcome?
   - `BasicCharacterTableau.DeserializeCharacterCode` now resets the preview when the race id is out of range or the mount material is missing. TAOM has custom races and creature mounts and a prefix on `BasicCharacterTableau.RefreshCharacterTableau` (Main/Features/HeroRace). Can a valid TAOM save now show a blank preview, or can the reset break TAOM's guard?
   - `MenuHelper.EncounterAttackConsequence` and `EncounterOrderAttack` skip `BeHostileAction.ApplyEncounterHostileAction` for a villager or caravan leader party. Does TAOM compensate anywhere for the old double penalty, or rely on that call for its supply caravans or relation features?
   - `SiegeAftermathCampaignBehavior` now resets and saves `_settlementProsperityCache`. TAOM's Patch84 can replace two of that behaviour's menu bodies and reads `_besiegerParty` by reflection.
8. Native pins. The change adds a v1.5.7 row to `KnownBuilds` in TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs and TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs, and to the pin tables in tools/tests/test_native_decompile.py and tools/tests/test_native_engine_methods.py. Are the rows consistent with each other and with the installed client DLL (14,219,104 bytes)? You may run `python tools/native_decompile.py --rva <rva>` and `--engine-method <name>`; they are read-only.
9. Completeness of the engine diff itself. Take a sample of vanilla assemblies and compare the two decompile folders yourself. Does the filtered diff hide a real change (a hunk dropped as encoding-only, as an assert line number or as a build number)? Did any changed member escape the record in docs/migration/v1.5.7-impact.md? Check TAOM's Harmony targets (docs/reference/taleworlds-api-snapshot/patch-targets.md; it records only the first target of a `TargetMethods()` patch), its GameModel overrides (gamemodel-bases.md), its reflection sites (reflection-sites.md) and its transpilers against the changed members.
10. Pins and statements. Is any statement of the current engine version still at v1.5.6 where it should move, or moved where the line is a dated record of a check made on v1.5.6? Is any claim in docs/migration/v1.5.7-impact.md false, unsupported or stronger than its evidence?

## Files

C#: Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs, Main/Features/KeepWardens/PatrolRosterHook.cs, Main/Features/DevConsole/Cheats/CareerPerkCheats.cs.
Tests: TAOM.Tests/Features/CulturalFeats/PartyMemberSizeLimitCallShapeTests.cs (new), TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs.
XML: Main/_Module/ModuleData/taom_module_strings.xml, the 12 Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml, Main/_Module/SubModule.xml. Live install, edited by this change: the Native `DependedModuleMetadata` row in "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\SubModule.xml" and "...\Modules\LOTRLOME_Armory\SubModule.xml".
Build: GameReferences.targets (the BUTR reference assembly version).
Tools: tools/check_handbook_attributes.py, tools/handbook_attribute_manifest.json, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py.
Docs and pins: .claude/pinned-game-version.txt, AGENTS.md, .claude/skills/engine-bump/SKILL.md, README.md, CONTRIBUTING.md, .github/pull_request_template.md, docs/migration/v1.5.7-impact.md and v1.5.7-diff-ranked.md (new), docs/migration/TRACKING.md, docs/migration/engine-bump-history.md, docs/INDEX.md, docs/reference/doc-lookup.md, docs/reference/bannerlord-engine-and-toolchain.md, development-machines.md, external-resources.md, bannerlord-animation-system-map.md, docs/reference/taleworlds-api-snapshot/gamemodel-bases.md and patch-targets.md, docs/modding/modules-overview.md, module-dependencies.md, docs/features/bannerlord-together-compat.md, coop-interop.md, coop-dedicated-server.md, docs/reviews/lessons/adapters-taleworlds-api.md.

## REQUIRED SECTIONS

### VANILLA CODE
Quote the v1.5.7 bodies you rely on (`DefaultPartySizeLimitModel.GetPartyMemberSizeLimit` and `CalculateMobilePartyMemberSizeLimit`, `PartyBase.PartySizeLimit`, `CampaignUIHelper.GetMainPartyHealthTooltip`, `SiegeAssaultEventComponent.OnPlayerEncounterContinueBattle`, the `IssuesCampaignBehavior.OnGameLoaded` migration, `HeroHelper.GetLastSeenText`) as code blocks with their file and line, and the v1.5.6 body wherever you claim a difference.

### Deep analysis
For suspects 1, 3, 6 and each bullet of 7, walk at least one concrete scenario (state the party, the settings, the save) to its result.

### CONFIG CROSS-REFERENCE
Check every id the change removes or references (string ids, localization keys, version strings, native addresses) against its definition and its consumers.

### FINDINGS OR OBSERVATIONS
One entry per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the defect, the concrete failure scenario, the proof (code you read), and the fix. Mark anything you could not prove UNVERIFIED. If you find nothing in a suspect, say what you checked.

## QUALITY GATES

- Cite file:line for every claim; a claim with no source is not a finding.
- Do not flag code that matches vanilla behaviour as a bug unless TAOM's intent says otherwise.
- Do not skip the hard sections (suspects 3, 7 and 9).
- Engine signatures come from the installed DLLs or the v1.5.7 decompile, never from memory.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
