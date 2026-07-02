ADVERSARIAL REVIEW: Patch55 BasicTableauRaceGuard refactor -- name-based per-race allow-list for the Save/Load hero preview.

FEATURE (1-2 lines): The Load Game screen preview previously coerced EVERY custom race to human (CTD guard, issue #299 -- the agentless native static-morph build AVs on morph-less custom heads, issue #295). Refactor: the guard now keys an EMPIRICAL allow-list by race NAME resolved via IRaceManager; "uruk" was render-verified in-game 2026-07-02 and allow-listed, so uruk saves preview true-to-race. Dwarf and all unverified races still coerce to human.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
NOTE: "rohan" is NOT a valid ID (Rohan uses "vlandia"). "dol_guldur" is NOT valid -- use "dolguldur".
RACE IDS (skins.xml, LOTRLOME_Armory): human(0, vanilla), dwarf, uruk, nazghul, orc, uruk_hai, berserker, cave_troll, hill_troll, pale_uruk, dg_uruk, goblin, saruman. Race INTS are skins.xml merge-order indices -- they shift with the module set; only NAMES are stable. Mordor CC races = ["uruk", "orc", "human"] (charactercreation/cultures.json). uruk_hai/pale_uruk/dg_uruk are DIFFERENT races from uruk and are intentionally NOT allow-listed.

READ FIRST:
- docs/features/hero-race.md -- sections "Save/Load Hero Preview CTD Guard (2026-06-24)" and "Per-race verification recipe (2026-07-02)"
- docs/reviews/rca-savetableau-2026-06-24.md -- the #299 timing RCA
- Main/Features/HeroRace/IBasicTableauRaceGuard.cs -- interface contract

CHANGED FILES (the diff under review):
- Main/Features/HeroRace/BasicTableauRaceGuard.cs -- the refactored guard (name-based TableauSafeRaceNames = {"uruk"}, OrdinalIgnoreCase; injects IRaceManager; validate-before-lookup; catch(Exception)->human fail-safe)
- Main/Features/HeroRace/HeroRaceIoC.cs -- registration comment update (guard eagerly resolved + patch Initialize)
- Main/Features/HeroRace/Hooks/BasicCharacterTableau_RefreshCharacterTableau_Patch.cs -- doc-comment update only (Prefix injecting ref int ____race unchanged)
- TAOM.Tests/Features/HeroRace/BasicTableauRaceGuardTests.cs -- 9 unit tests (NSubstitute IRaceManager)
- TAOM.Tests/Features/HeroRace/Patch55BasicTableauRaceGuardBindingTests.cs -- NEW drift-guard pinning BasicCharacterTableau._race as int via AccessTools.TypeByName
- docs/features/hero-race.md + CHANGELOG.md -- docs

DEPENDENCY FILES (unchanged, read for context):
- Main/Core/Domain/RaceManager.cs + IRaceManager.cs -- id<->name maps built ONCE (lazy, latching) from FaceGenAdapter.GetRaceNames()
- Main/Adapters/FaceGenAdapter.cs -- passthrough to TaleWorlds.Core.FaceGen.GetRaceNames()
- Main/SubModule.cs lines ~274-297 -- Patch55 category applied in OnBeforeInitialModuleScreenSetAsRoot, one-shot flag _basicTableauGuardApplied
- Main/IoC.cs -- RegisterCoreServices (IRaceManager) runs before HeroRaceIoC.RegisterHeroRaceFeature

VANILLA CODE (decompile these from the INSTALLED game and paste relevant bodies as code blocks):
- TaleWorlds.MountAndBlade.View.Tableaus.BasicCharacterTableau -- from "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll" (NOT in the root bin; NOT reliably in E:/Decompiled_Bannerlord categorized folders). Focus: DeserializeCharacterCode (the pipe-format visual code, _race at index 4) and RefreshCharacterTableau (SkinGenerationParams ctor arg order -- confirm _race is passed where we think; the hardcoded human AnimationSystemData).
- TaleWorlds.MountAndBlade.GauntletUI.TextureProviders.SaveLoadHeroTableauTextureProvider -- same Modules/Native bin, TaleWorlds.MountAndBlade.GauntletUI.dll. Confirm sole instantiation of BasicCharacterTableau.
- SandBox.View.MainHeroSaveVisualSupplier.GetMainHeroVisualCode -- "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/bin/Win64_Shipping_Client/SandBox.View.dll". This writes the race int into save metadata at SAVE time.
- TaleWorlds.Core.FaceGen (root bin TaleWorlds.Core.dll) + TaleWorlds.MountAndBlade.FaceGen and CoreManaged.OnLoadCommonFinished (root bin TaleWorlds.MountAndBlade.dll) -- the GetRaceNames null-tolerance and CreateInstance timing.

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with evidence):
S1. CROSS-SESSION RACE-INDEX DRIFT: the save's visual code stores the race as an INT written under the module set active at SAVE time. The load screen maps that int to a name under the CURRENT module set. If the skins.xml merge order changed between sessions (module added/removed/reordered), the int can map to a DIFFERENT race name -- worst case an unsafe race's int now maps to "uruk", passes the allow-list, and feeds the native build -> potential CTD. Question: is there any version/etc. field in the visual code that invalidates cross-session indices (we saw only a leading "4|" format version)? Is this residual risk acceptable/vanilla-equivalent (vanilla feeds the raw int to the native build regardless), or does it warrant hardening (e.g., only pass through when the CURRENT session's uruk id equals the save's int -- which is exactly what the name lookup already does... think carefully about what the guard CAN and CANNOT know about the save's original module set)?
S2. SINGLE-SAMPLE EMPIRICAL VERIFICATION: uruk was verified safe by rendering ONE save (male uruk, one equipment set, one BodyProperties). The native static-morph AV (#295) is per-MESH (dwarf head.eye had 0 morph targets). All uruk skins in skins.xml share face_meta_mesh="sk_uruk_basemesh_a_body/_head" across genders/maturity -- but HAIR/BEARD meshes are selected per-save from BodyProperties and are also morph-deformed (deform_keys carry deforms_hair factors). Could a different uruk save (different hair/beard index, female, child/teen maturity) pull a morph-less hair/beard mesh and still AV? Read the uruk race's hair_meshes/beard_meshes in skins.xml (E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/skins.xml, race id="uruk" starts line ~14953) and reason about whether the static-morph path touches them. If the residual risk is real, say what evidence would close it (render-test matrix? asset audit?).
S3. RACEMANAGER INIT-LATCH: RaceManager.EnsureInitialized latches _initialized=true permanently even when FaceGen.GetRaceNames() returns null (human-only fallback map, Reuse.Singleton lifetime). Claude's analysis: no current caller can reach it before the native OnLoadCommonFinished installs the FaceGen instance, so the latch never fires early today. CONFIRM or DISPUTE by enumerating call paths (all IRaceManager consumers are constructor-injected; first method call is the guard's ResolveSafeRace on the Load Game screen).
S4. CATCH-ALL EXCEPTION HANDLER: ResolveSafeRace wraps the resolution in catch(Exception)->HumanBaseRace, justified as a crash-guard boundary (a throw in the Prefix would propagate into the cold-menu render tick). Dispute if you think this masks defects in a way the justification does not cover, or if a narrower catch is strictly better here.
S5. HARMONY PREFIX SEMANTICS: the Prefix mutates ref int ____race and returns void. Confirm a void Prefix cannot skip the original and that mutating the injected field ref writes back BEFORE the original method body reads _race (Harmony writes injected ref fields back after the prefix completes, before original execution). If the write-back timing were wrong the whole guard would be a no-op -- but the pre-refactor guard demonstrably worked in-game (human previews on custom-race saves), so weigh that evidence.
S6. TEST FIDELITY: the 9 unit tests mock IRaceManager. Is any test asserting behavior the REAL RaceManager cannot exhibit (e.g., GetRaceNameFromId returning a name for an id IsValidRaceId rejected -- the real one returns "human" fallback there)? Flag any mock-reality divergence that could hide a real-world path.

FEATURE-SPECIFIC DEEP ANALYSIS (concrete scenarios to walk):
A. Player saves an uruk campaign, then disables LOTRLOME_Armory (or adds a mod inserting a race before uruk), opens Load Game. Walk the exact data flow and state what renders / what could crash, with the guard as written.
B. A save whose visual code has a corrupt/garbage race int (e.g., 999). Walk IsValidRaceId -> coerce path and confirm no lookup of GetRaceNameFromId happens (validate-before-lookup).
C. The ESC-menu in-game Save/Load screen (campaign running). Same guard path? Any difference vs cold menu (FaceGen instance definitely set; RaceManager may already be initialized by other consumers)?
D. A FEMALE uruk save on the list (underwear_female, same head mesh). Does the SkinGenerationParams gender flag change which meshes the native build morphs?

CONFIG CROSS-REFERENCE:
- TableauSafeRaceNames contents vs skins.xml race ids (exact string "uruk").
- charactercreation/cultures.json mordor races list.
- Confirm no OTHER TAOM code hardcodes race INTS in a way this refactor makes inconsistent (grep for HumanBaseRace, race == 0, hardcoded race indices).

FINDINGS OR OBSERVATIONS: report every defect with file:line, severity (P1 blocking / P2 should-fix / P3 nit), a concrete failure scenario, and a proposed fix. If a Known Suspect is DISPUTED, show the disproving code. If you find nothing in a section, write "No findings" -- do not skip sections.

QUALITY GATES:
- Paste decompiled vanilla code for every engine claim (BasicCharacterTableau, the texture provider, MainHeroSaveVisualSupplier).
- Cross-reference every race string against skins.xml.
- Do not flag vanilla-matching behavior as a TAOM bug.
- Distinguish residual risks accepted by design (documented in hero-race.md) from new defects introduced by this diff.

PRIOR REVIEW LESSONS:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught the Patch55 timing bug itself (Codex C1, #299). FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Write your review to: docs/reviews/codex-adversarial-patch55-race-allowlist-2026-07-02.md (you are already producing stdout that is redirected there -- just emit the review as your output).
