# Codex Adversarial Review -- Custom Battle Curated Commander Lists (TAOM)

You are an adversarial code reviewer. Assume the code has bugs and prove it. Be concrete: cite file:line, decompile or read the vanilla target before claiming a finding, and mark each finding CONFIRMED or DISPUTED with evidence. ~95% of confident-sounding findings are real; the other 5% are false positives, so verify against actual source, not plausibility.

## Feature description

Custom Battle commander dropdowns previously showed the first 3 lords of a culture sorted alphabetically by id, and the eligibility regex `^lord_[A-Za-z0-9]+_[A-Za-z0-9]+$` (2-segment only) made 3-segment ids unreachable. This change adds a data-driven config that maps each faction (culture StringId) to an ordered, curated list of lord ids; a configured faction returns that exact list, bypassing the regex, the cap, and the culture filter. It also reassigns 3 lesser Nazgul (lord_1_48_1/2/3) from dolguldur to mordor culture.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid culture id -- Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- docs/features/custom-battles.md (the feature doc, esp. the "Curated commander lists" + "Commander filter+cap" sections)
- Main/_Module/ModuleData/custom_battle/custom_battle_commanders.json (the new config)
- The whole pipeline: Main/Features/CustomBattles/CustomBattleService.cs, ICustomBattleService.cs, CustomBattlesIoC.cs, Main/Features/CustomBattles/Config/*.cs, Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs + ISideCommanderFilter.cs + CommanderSelectorRebuilder.cs + CustomBattleCommandersHook.cs + CustomBattleSideVM_OnCultureSelection_Patch.cs + CustomBattleSideVM_RefreshValues_Patch.cs + CustomBattleSideVM_OnCharacterSelection_Patch.cs + CustomBattleSideVM_UpdateCharacterVisual_Patch.cs + CustomBattleData_Characters_Patch.cs
- Main/Adapters/IObjectManagerAdapter.cs + ObjectManagerAdapter.cs (CharacterInfo/CultureInfo DTOs, GetBasicCharacter, GetAllCharacterInfos)
- The 3 culture edits: Main/_Module/ModuleData/characters/lords.xml (lord_1_48_1/2/3) + Main/_Module/ModuleData/lords.xslt (templates for the same 3 ids)

## Known Suspects (CONFIRM or DISPUTE each, with evidence)

1. Cross-culture / cross-faction commander acceptance. The config lists lords whose own culture differs from the faction key on purpose: Khamul (lord_1_48, Culture.dolguldur) and the 3 lesser Nazgul under "mordor"; Duinhir (lord_WE9_l, Culture.empire) under "gondor". When the player picks the Mordor faction and selects Khamul as the side general, does ANY downstream code assume the selected commander's culture matches the side's faction culture (banner, equipment roster, team setup, OnCharacterSelection, battle start)? Trace CustomBattleSideVM.OnCharacterSelection and the battle-start path (BannerlordMissions.OpenCustomBattleMission / CustomBattleCombatant.SetGeneral). Hypothesis: harmless. Prove or disprove.

2. All-unresolvable curated faction -> empty dropdown. The provider validates id SYNTAX at load but not RESOLVABILITY (no live MBObjectManager). SideCommanderFilter resolves each id via GetBasicCharacter and warns+skips nulls. If EVERY id in a curated faction fails to resolve, ResolveCommandersForCulture returns an empty list and CommanderSelectorRebuilder.Apply early-returns (commanders.Count == 0), leaving the dropdown in whatever prior state it had. Does that produce a stale/mismatched dropdown, a null SelectedCharacter NRE, or a silent wrong-list? Verify the existing OnCharacterSelection/UpdateCharacterVisual null-guards actually cover this. Hypothesis: guards cover it. Prove or disprove.

3. Master-list vs dropdown decoupling. GetCommanderIds() (feeds CustomBattleData.Characters via CustomBattleCommandersHook) is intentionally UNCHANGED and still regex-filters out 3-segment ids. The curated 3-segment ids (lord_1_48_1/2/3, lord_4_3_1, lord_4_3_2) only appear via the per-faction dropdown rebuild (CommanderSelectorRebuilder, resolving from GetBasicCharacter). Is there ANY path where a selected commander MUST also be present in CustomBattleData.Characters -- e.g. validation on battle start, the SelectorVM re-deriving from the master list on a later refresh, or save/serialize of the chosen general? If so, a curated 3-segment general could be silently dropped or NRE. Hypothesis: fully decoupled. Prove or disprove.

4. HasCuratedEntry / GetCuratedCommanderIds two-call consistency. CustomBattleService.GetCommanderIdsForFaction calls provider.HasCuratedEntry(factionId) then provider.GetCuratedCommanderIds(factionId). Both guard null/whitespace and both hit a single OrdinalIgnoreCase Lazy dictionary. Is there any input (whitespace, case, a key only present after sanitization) where HasCuratedEntry returns true but GetCuratedCommanderIds returns empty, or vice versa? Check the provider's HasCuratedEntry whitespace short-circuit vs the lazy-load trigger. Hypothesis: consistent. Prove or disprove.

5. Config DTO nullability / parse edge cases. CustomBattleCommandersConfig.Factions is nullable with no initializer (so an absent "factions" key deserializes to null and the provider warns). Verify: (a) JSON with "factions": null, (b) "factions": {}, (c) a faction value that is null or [], (d) the leading "_comment" root field -- none should NRE; each should land on the documented behavior (default fallback / info log). Read CustomBattleCommandersProvider.Load + Validate.

6. Nazgul culture edit ripple (lord_1_48_1/2/3 dolguldur -> mordor). These 3 heroes belong to Dol Guldur clans but now carry mordor PERSONAL culture. Independently verify nothing dolguldur-culture-keyed breaks: VolunteerRecruitmentService pools (clan-keyed or culture-keyed?), child-generation exclusion (does it exclude BOTH mordor and dolguldur?), NazgulFamily INazgulRegistry (id-keyed?), cultural feats, clan/culture consistency assumptions, and SAVE-COMPAT (Hero.Culture is an engine-saved field -> existing saves keep dolguldur; is that a problem anywhere?). Also confirm lords.xml and lords.xslt agree (both emit Culture.mordor for all 3) and that the engine's effective culture is mordor (which source wins in TAOM's load pipeline -- the lords.xml NPCCharacter def or the lords.xslt template?).

## Files changed in this commit (656daae8)

New:
- Main/Features/CustomBattles/Config/CustomBattleCommandersConfig.cs
- Main/Features/CustomBattles/Config/ICustomBattleCommandersProvider.cs
- Main/Features/CustomBattles/Config/CustomBattleCommandersProvider.cs
- Main/_Module/ModuleData/custom_battle/custom_battle_commanders.json
- TAOM.Tests/Features/CustomBattles/CustomBattleCommandersProviderTests.cs
- TAOM.Tests/Features/CustomBattles/CuratedDropdownIndependenceTests.cs

Modified:
- Main/Features/CustomBattles/CustomBattleService.cs (curated branch in GetCommanderIdsForFaction)
- Main/Features/CustomBattles/CustomBattlesIoC.cs (provider registration)
- Main/Features/CustomBattles/ICustomBattleService.cs (doc on takeMax semantics)
- Main/Features/CustomBattles/Hooks/SideCommanderFilter.cs (warn+skip on null resolution)
- Main/_Module/ModuleData/characters/lords.xml + lords.xslt (3 culture edits)
- TAOM.Tests/Features/CustomBattles/CustomBattleServiceTests.cs (+6 curated-branch tests)
- docs/features/custom-battles.md

## VANILLA CODE to inspect

You have the installed game DLLs and a decompile dump. Decompile/read these and paste the relevant bodies as evidence for Suspects 1-3:
- TaleWorlds.MountAndBlade.CustomBattle CustomBattleSideVM.OnCharacterSelection + OnCultureSelection + RefreshValues + UpdateCharacterVisual (installed CustomBattle module DLL)
- TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM<T>.Refresh (TaleWorlds.Core.ViewModelCollection.dll)
- TaleWorlds.MountAndBlade.BannerlordMissions.OpenCustomBattleMission + CustomBattleCombatant.SetGeneral (TaleWorlds.MountAndBlade.dll)
Decompile from the installed DLLs at: E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\ (authoritative, v1.4.6). The dump at E:\Decompiled_Bannerlord\ is v1.4.5 -- use it only for browsing, not final signatures.

## CONFIG CROSS-REFERENCE (mandatory)

For custom_battle_commanders.json: confirm every faction key is a real selectable culture StringId (note vlandia=Rohan) and every one of the ~43 lord ids resolves to a real NPCCharacter definition in Main/_Module/ModuleData/characters/lords.xml OR a template in Main/_Module/ModuleData/lords.xslt. Flag any id that does not exist (it would silently drop at runtime). The ids include 2-segment (lord_1_17) and 3-segment (lord_1_48_1, lord_4_3_1, lord_4_3_2) forms.

## REQUIRED OUTPUT SECTIONS

1. KNOWN SUSPECTS -- one CONFIRMED/DISPUTED verdict per suspect (1-6), each with file:line + vanilla evidence where relevant.
2. CONFIG CROSS-REFERENCE -- table of faction keys + id existence results.
3. FINDINGS -- numbered, each: severity (HIGH/MED/LOW), file:line, what is wrong, why, suggested fix. HIGH = crash / silently-wrong-behavior / data corruption.
4. THINGS THE IMPLEMENTER MAY HAVE MISSED -- anything outside the suspects (fail-safe defaults, lifecycle/stale state, save-compat, convention drift, test gaps).
5. OBSERVATIONS -- non-blocking notes.

## QUALITY GATES

- Do not flag vanilla-matching code as a bug -- if TAOM mirrors vanilla behavior, that is intentional.
- Do not assume an id is wrong without grepping for it. "I did not find X" must be backed by an actual search.
- Decompile before asserting a Harmony/VM/engine-interaction finding.
- This change adds NO new Harmony patch and NO new GameModel -- if you think it should have, say so as an observation, not a bug.
- Prior-review lessons: Codex once assumed empire=Rohan (it is Dunland) and flagged vanilla-matching code as bugs. Avoid both.
