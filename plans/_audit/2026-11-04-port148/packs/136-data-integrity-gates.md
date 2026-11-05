# Input pack: WP 136 data-integrity-gates

Title (draft): Validator and test gates for XML schemas, data references, banners, factions and generated blocks
Wave: 5   Lane: A   Draft priority: P2   Groups: 15   Port commits: 21
Depends on (WP ids): 108, 117, 131, 133

## Orchestrator notes for this WP (binding)
- tools/validate_moduledata.py is serial: WP108's collision pass first, then fa8c06f82's SCHEMA_INVALID hand-merged (tool half only; its lens half is WP138), later codes after.
- K2: 569f6750d's tool-comment and rule-row rewording plus the uruk-hai skirt RCA (no hill_troll wording).
- XML-DATA-02: the validator header prints each extra module's SubModule.xml Version and newest file date.
- K1: 1ebcf86a8: its test test_item_registry_counts_only_items_documents calls vm.armour_acquisition_ref_issues, defined only by SKIP-FEATURE 04da4a0d0: drop that call and the armour_acquisition fixtures; use a non-Items fixture shaped like culture_marketplace_config.
- K1: 8caac7da5 (tests-architecture-gates): ServiceSignatureGateTests uses TaomAssemblies.All (from a15563a3c, absent on 1.4.x): scan the TAOM assembly instead.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Deep-dive findings and non-port items this WP owns
- XML-DATA-05 [P3/high] The prefab budget gate hardcodes the desktop path and counts one module; the 131,072 cap is an editor-only hazard | port:   (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)
- XML-DATA-07 [P3/high] No engine-XSD gate on 1.4.x; the 1.5.x tool runs unchanged against v1.4.8 and finds the 1.4.x data clean | port: fa8c06f82,709649c3f,48d66b59a,c1ce78207,bc30b76da,09f3126a7,f1675c058  (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)
- XML-DATA-09 [P3/high] The 1.4.x generated-block upsert can corrupt a ModuleData file; f963c6188's marker-region guard ports cleanly | port: f963c6188,bd457ee6e  (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)
- XML-DATA-10 [P3/medium] Remaining rgl asset warnings are vanilla or cosmetic; audit_scene_names.py reports 9 false crash suspects | port:   (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)

## Port groups owned by this WP (from the commit classification)

### group `culture-xml-data-gates`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
symptoms: crash
- `d28d9a7d3` 2026-09-14 PORT-PARTIAL P2 pick=conflict :: fix(cultures): v2.0.28 - every culture names its executioner
  - why: The executioner attribute serves a v1.5.0 engine field, but CultureXmlCommentPlacementTests guards a culture-parse crash class that also exists on v1.4.8.
  - port notes: Take TAOM.Tests/Core/CultureXmlCommentPlacementTests.cs. Verified in _categories_v1.4.8: CultureObject.cs:419-423 walks notable_templates (and the other child lists) by XmlNode and reads .Attributes, and MBObjectManager.LoadXmlWithValidation (:1061-1121) sets no IgnoreComments, so a comment inside a child list NREs and aborts the file on v1.4.8 too. origin/bannerlord-1.4.5 taom_spcultures.xml has 0 such comments today, so it lands green; CultureDataFixture exists there. Leave the executioner attributes (taom_spcultures.xml, spcultures.xslt) and CultureExecutionerTests: v1.4.8 CultureObject has no Executioner and the installed XmlSchemas/SPCultures.xsd declares no executioner attribute, so the data would only add 'attribute is not declared' validation errors (the same class as evidence item 8). NotableTemplateGenderTests is optional: its gender rule exists for the 1.5-only ASO Trader start.
  - issues: 484

### group `xslt-template-coverage-gate`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
- `bc5b5d71d` 2026-09-14 PORT-PARTIAL P2 pick=conflict :: fix(engine): v2.0.28 - recruitment perks per v1.5.2, dead faction data
  - why: The recruitment-perk rework and faction-string deletion follow v1.5.2 engine changes, but XsltTemplateCoverageTests and the removal of three heroes.xslt templates that never matched on any version apply to 1.4.x.
  - port notes: Take TAOM.Tests/Migration/XsltTemplateCoverageTests.cs and the deletion of the Hero[@id='lord_1_9_5'], 'lord_1_52_4' and 'lord_1_71_1' templates (present on origin/bannerlord-1.4.5 heroes.xslt:612, 817, 903) with their taom_xslt_strings.xml rows and 12 std_taom_xslt_strings translations. Leave: TaomPartyWageModel/WageModifierService perk routing (v1.4.8 DefaultPartyWageModel.GetTroopRecruitmentCost:269 still gates Frugal on buyerHero.IsPartyLeader, so the 1.4.x shape matches its engine); the 44 str_adjective_for_faction/str_short_term_for_faction overrides and 16 module_strings.xslt templates (v1.4.8 Helpers/FactionHelper.cs still reads them and Native module_strings.xml still ships them); the revolt-tuning and cultural-feats doc edits (v1.5.x facts). Run the gate on 1.4.x before relying on it: it may flag other dead templates.

### group `issue-621-deep-review-xsd-gate`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
- `fa8c06f82` 2026-09-18 PORT P2 pick=conflict :: chore(harness): v2.0.29 - /deep-review senior lenses, XSD gate (#621)
  - why: Adds tools/validate_xml_schemas.py (every engine-loaded ModuleData file checked against the engine's own XSD, run by validate_moduledata.py as SCHEMA_INVALID and so by the commit hook) and rebuilds /deep-review as per-lens read-only reviewer agents; neither exists on the 1.4.x tip.
  - port notes: The tool's file and schema resolution was read from the v1.5.3 decompile (MBObjectManager.GetMergedXmlForManaged, XmlResource.GetXmlListAndApply, ModuleHelper.GetXmlPath/GetXsdPath): re-check on the v1.4.8 dump before trusting its exit code. Expect findings on the first --live run against a 1.4.8 install; this is the gate that flags evidence item 8's class (an attribute such as taom_body_length that the 1.4.8 schema does not declare). tools/validate_moduledata.py conflicts on context with ef6afeb67's collision pass: apply that first or hand-merge. Pin .claude/agents/deep-reviewer.md to claude-opus-5-5 as c79a58521 later did, not Fable. CHANGELOG, CLAUDE.md, INDEX and lessons are bookkeeping conflicts; lxml stays optional (exit 2 without it).
  - conflicts: tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
  - issues: 619, 620, 621, 622

### group `validator-item-registry-items-root`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
- `1ebcf86a8` 2026-09-28 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.32 - the item registry reads only Items documents
  - why: The validator's item registry counted <Item id> rows in any XML file as item definitions; the fix reads only documents with an <Items> root. The same gap exists on 1.4.x, where culture_marketplace_config.xml and Native/mpcosmetics.xml (488 rows, root Cosmetics) both feed the registry.
  - port notes: Take from tools/taom_schema.py: _DOC_ROOT_RE, _ITEM_DOC_ROOTS, the doc_roots parameter of _scan and its use in build_registries; plus the test test_item_registry_counts_only_items_documents. Leave the LordsLadder, LordsMaterials and LadderWeapons checks with their tests (armour feature), and the LookupError/OSError hardening, which lives inside armour_acquisition_ref_issues (absent on 1.4.x). On origin/bannerlord-1.4.5, taom_schema._scan reads every *.xml under the item roots (Native included). Measured read-only on this laptop (working tree plus installed modules): all 116 item documents have an <Items> root and no id is defined only by a non-Items document, so the rule drops no legitimate definition. But the origin tip's marketplace config blacklists sk_mordor_mouth_of_sauron_helm (d906fc921), which no Items document in this laptop's Armory, TAOM_Map or working tree defines; today that config row counts as its definition. Re-run python tools/validate_moduledata.py after the port to see what surfaces.
  - conflicts: tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py

### group `custom-battle-scene-gate`  P2  track=data-integrity  commits=2  sim: clean=1 conflict=1
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs, tools/generate_enlistment_rosters.py, tools/tests/test_generate_enlistment_rosters.py
- `d96167efa` 2026-09-30 PORT-PARTIAL P2 pick=clean :: feat(custom-battles): v2.0.32 - Edoras siege in the battle picker
  - why: The CustomBattleSceneLiveDataTests gate is a data-integrity check worth having on 1.4.x: every picker row must name a shipped scene, and every siege scene must declare its siege levels. The Edoras row itself is content.
  - port notes: Take TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs in its babcb8335 form (it parses the level declarations) and the file-catalogue line. Leave the Edoras row in custom_battle_scenes.xml: its scene ships only in the unversioned TAOM_Map. The levels it requires match v1.4.8, where BannerlordMissions.OpenSiegeMissionWithDeployment builds level_1, level_2 or level_3 plus ' siege'. It is a LiveInstall test, so it reports Inconclusive without TAOM_Map installed.
- `babcb8335` 2026-09-30 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.32 - Codex review fixes for the KEYforce art (#696)
  - why: These Codex fixes harden the Custom Battle scene gate and add a general testing lesson plus reviewer guidance. The enlistment generator half serves only the 1.5.x creature-rider filter.
  - port notes: Take the CustomBattleSceneLiveDataTests rewrite (element parsing, siege plus level_1 to level_3, and two fragment tests that run without the game), the testing-qa.md lesson 'A data gate checks what the engine asks for', and the two .ai/review-reference.md entries. Leave the generate_enlistment_rosters.py rework and its test: they are built around 1d8fc1d8e's filter, and 1.5.x reports no roster change from them. Also leave the RCA, REVIEW-LOG, codex-track-record and prompt records.
  - prereqs: d96167efa
  - conflicts: TAOM.Tests/Features/CustomBattles/CustomBattleSceneLiveDataTests.cs, tools/generate_enlistment_rosters.py, tools/tests/test_generate_enlistment_rosters.py
  - issues: 696

### group `faction-card-feat-gate`  P2  track=data-integrity  commits=2  sim: clean=0 conflict=2
- `e166d8c84` 2026-10-18 PORT-PARTIAL P2 pick=conflict :: feat(balance): v2.0.34 - cap evil party-size feats at 10%
  - why: The balance numbers are out of scope, but its new test comparing every faction card's '+N% party size' with the production feat catches a live 1.4.x defect: Mordor's card says +10% while the feat is 20%.
  - port notes: Take FactionMapDataTests.FactionCard_PartySizePercent_MatchesProductionFeat and the TaomCulturalFeatsDefinitionTests.ProductionEffectBonus helper it calls (added by this commit), run against 1.4.x values; all 12 faction keys exist in the 1.4.x factions.json. On origin/bannerlord-1.4.5 it fails only for dominion_of_mordor (factions.json:1045 '+10% party size limit' against TaomCulturalFeats.cs:912-915 0.2f; the other 11 cards match their feats), so fix that card text and its 12 translations in the same change. Leave every feat value, garrison number, string retune and doc change.
  - issues: 818
- `6553b8c70` 2026-10-18 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - RCA addendum for the party-size cap
  - why: RCA addendum for the party-size cap whose one lesson line records the faction-card gate that e166d8c84's partial port brings.
  - port notes: Only with e166d8c84's gate: add the data-content-cultures.md 'Gated since' lesson reworded for 1.4.x (the Mordor card mismatch dates from 4f72e160e). Leave the gamemodels-services.md status line (it describes the 1.5.x 10% cap) and the RCA addendum.
  - prereqs: e166d8c84
  - issues: 818

### group `issue-949-faction-ref-validator`  P2  track=data-integrity  commits=2  sim: clean=0 conflict=2
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/HeroFaceAdapter.cs, tools/taom_schema.py, tools/test_hooks.sh, tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
- `ca92db878` 2026-10-20 PORT-PARTIAL P2 pick=conflict :: feat(validator): v2.0.35 - every Faction reference must name a clan
  - why: Adds BROKEN_FACTION_REF to validate_moduledata.py (a gate for the wrong-XML-data class the maintainer has seen behind 1.4.8 infinite loads) and makes a validator crash exit 3 so the commit hook fails open with the traceback instead of blocking with an empty reason.
  - port notes: Adapt rather than cherry-pick: the 1.4.x copies of taom_schema.py, validate_moduledata.py, test_validate_moduledata.py, test_hooks.sh, check-moduledata-validation.sh and the two docs lack about 4,800 lines that the pre-image has, but RefKind (1.4.x taom_schema.py:143), REF_KINDS (:162), _scan_files (:1709), build_registries (:2149) and the size floors all exist. Carry the faction RefKind row, _CLAN_DEF_RE and _CLAN_STRIP_RE, the factions registry with both floors, the exit-3 entry() and import-guard wrapper, the hook's exit-3 allow path with its new test_hooks.sh cases, the new validator tests, and the taom_query/MCP prefixes. TAOM_Map is already an extra ref module on 1.4.x (validate_moduledata.py:78), so the gate will also cover the live map's settlement owners. A read-only check of this laptop's installed set found no undefined Faction refs, so expect the gate to pass on 1.4.x data.
  - conflicts: tools/taom_schema.py, tools/test_hooks.sh, tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
  - issues: 949
- `2225939cd` 2026-10-20 PORT-PARTIAL P3 pick=conflict :: docs(review): v2.0.35 - review record for the Dale follow-ups
  - why: Review record for #949 to #952: its validator, banner-gate and hook findings (F1, F7, F8, F9) and two generic lessons describe work that ports, while its face-variety parts do not.
  - port notes: Take the RCA (or its #949, #951 and hook rows), the REVIEW-LOG row, the testing-qa lesson 'A gate counts what it parsed against what exists' and the build-tooling-workflow lesson 'Give a scratch transform a timeout'. Optional: the generic-but-#952-sourced lessons (fakes from the engine's inputs; stateless singleton adapters). Leave the HeroFaceAdapter.cs comment, the lord-face-variety.md and lords-and-heroes.md wording, and the campaign-mechanics new-game hero pass lesson.
  - prereqs: ca92db878, 487e3d9a9
  - conflicts: Main/Adapters/HeroFaceAdapter.cs
  - issues: 949, 952

### group `issue-951-banner-icon-gate`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
- `487e3d9a9` 2026-10-20 PORT-PARTIAL P2 pick=conflict :: fix(banners): v2.0.35 - seven clan banners lose blank and debug layers
  - why: All seven broken clan banner keys (unknown icon ids that draw nothing, colour ids drawn in the engine's 0xDEADBEEF debug colour) exist verbatim on 1.4.x, and the new tools/tests/test_banner_icon_ids.py gate reads only files present there.
  - port notes: Take the clans.xml and spclans.xslt fixes (they merge cleanly), docs/modding/banners-and-heraldry.md and tools/tests/test_banner_icon_ids.py. Drop the docs/features/dale-lord-expansion.md and lord-banners.md hunks (1.5.x-only docs). Run the gate on 1.4.x data: it may find further broken keys there. It needs the game install for Native banner_icons.xml (through tools/_gamedir.py, present on 1.4.x).
  - issues: 951

### group `tools-validate-xml-schemas`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Dependencies/Foundation/PatchShield.cs, TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs, tools/tests/test_validate_moduledata.py, tools/tests/test_validate_xml_schemas.py, tools/validate_moduledata.py, tools/validate_xml_schemas.py
- `48d66b59a` 2026-10-21 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - schema check names language files and stylesheets
  - why: Stops tools/validate_xml_schemas.py reporting language files and stylesheets as NOT REGISTERED, and corrects the PatchShield value-finalizer comment; the validator is absent on 1.4.x (added by the #621 XSD gate, fa8c06f82), but the wrong comment is at 1.4.x PatchShield.cs:251.
  - port notes: Can land now: the PatchShield finalizer comment fix, the Harmony 2.4.2 behaviour-pin test, and the harmony lesson. The test file was created by 5ee49d05f; create it fresh if that commit is not ported. Needs fa8c06f82: the language-file and stylesheet classes in validate_xml_schemas.py, its tests, the rerun-hint fix in validate_moduledata.py, and the XML deep-review lens. The line-ending measurements in TRANSLATOR_GUIDE, localization-map, xml-data.md and editing-safely were taken on 1.5.x files: re-measure on 1.4.x before porting those sentences.
  - prereqs: fa8c06f82, 5ee49d05f
  - conflicts: Dependencies/Foundation/PatchShield.cs, TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs, tools/tests/test_validate_moduledata.py, tools/tests/test_validate_xml_schemas.py, tools/validate_moduledata.py, tools/validate_xml_schemas.py

### group `tools-generator-marker-region`  P2  track=dev-tools  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/generate_arthedain_troops.py, tools/generate_new_faction_kingdoms.py, tools/promote_borrowed_cultures.py, tools/tests/test_generate_arthedain_troops.py, tools/tests/test_generator_regions_in_sync.py
- `f963c6188` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - one guard for generated XML blocks
  - why: Adds one marker-region writer that refuses duplicate or damaged generated blocks; 1.4.x's tools/generate_new_faction_kingdoms.py still strips and re-appends its block (upsert, lines 97-102) and cannot catch a stale second copy, which the engine loads last.
  - port notes: Take tools/_marker_region.py and tools/tests/test_marker_region.py, and switch the 1.4.x generate_new_faction_kingdoms.py upsert to replace_region (this subsumes the #947 in-place fix bd457ee6e). The 1.4.x script has no --only/--check (about 246 changed lines between the lines), so adapt by hand, not by cherry-pick. Leave tools/generate_arthedain_troops.py and tools/tests/test_generate_arthedain_troops.py: Arthedain is 1.5.x-only (b02467744). Low value unless these generators are re-run on 1.4.x.
  - prereqs: bd457ee6e
  - conflicts: tools/generate_arthedain_troops.py, tools/generate_new_faction_kingdoms.py, tools/tests/test_generate_arthedain_troops.py
  - issues: 975
- `c73cf2961` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - culture generators keep blocks, never half-write
  - why: insert_new_factions.py and promote_borrowed_cultures.py exist on 1.4.x and still strip-and-append their regions and write each file before parsing it, so one bad output leaves a broken file on disk and the rest unwritten.
  - port notes: insert_new_factions.py is identical on both lines before this commit, so its hunk (replace_region plus refuse_unparsable before any write) applies as is. promote_borrowed_cultures.py differs from 1.4.x by about 390 changed lines (Arthedain-era work), so port its upsert and unparsable() parse-before-write by hand. Take tools/tests/test_region_writers_in_place.py. Leave tools/tests/test_generator_regions_in_sync.py (1.5.x-only; it runs the Arthedain generators).
  - prereqs: f963c6188
  - conflicts: tools/promote_borrowed_cultures.py, tools/tests/test_generator_regions_in_sync.py
  - issues: 976, 979
- `b409e6505` 2026-10-23 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.35 - sweep follow-ups, stale and wrong docs corrected
  - why: Docs follow-up to the 2026-10-23 sweep; on 1.4.x only the parts describing the generator guard and the XSD duplicate-id check apply, and only once those tools are ported.
  - port notes: Take the two tools/README.md bullets (marker region through _marker_region.replace_region; parse every output before writing any), with f963c6188/c73cf2961. Take the new #974 lesson in docs/reviews/lessons/build-tooling-workflow.md and the docs/features/moduledata-validation.md paragraph only after fa8c06f82's SCHEMA_INVALID lands. Leave .claude/rules/adapters.md (the relations bullet it edits is absent from 1.4.x's adapters.md, and AffectRelativesGateTests #923 is absent on 1.4.x), the #947 lesson edit and the testing-qa.md edit (both lessons are 1.5.x-only), recipe-add-a-kingdom.md and new-factions-misty-mountains-lindon.md (Arthedain-specific), and rca-ui-data-sweep-2026-10-23.md.
  - prereqs: f963c6188, c73cf2961, 4644bf493
  - issues: 974, 975, 976, 977, 979

### group `tools-xsd-schema-gate`  P2  track=tests-ci  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/tests/test_validate_xml_schemas.py, tools/validate_xml_schemas.py
- `4644bf493` 2026-10-23 PORT P2 pick=conflict :: test(tools): v2.0.35 - pin the engine check for repeated clan ids
  - why: Pins that the XSD gate catches a repeated clan, hero or kingdom id, which holds on 1.4.x: the v1.4.8 install's XmlSchemas declare xs:unique on @id (Factions.xsd:134, Heroes.xsd:24, Kingdoms.xsd:139) and 1.4.x SubModule.xml registers Factions, Heroes and Kingdoms at characters/clans, characters/heroes and taom_spkingdoms.
  - port notes: Needs the SCHEMA_INVALID gate from fa8c06f82: tools/validate_xml_schemas.py and tools/tests/test_validate_xml_schemas.py are absent on 1.4.x. registered_files already returns (entries, missing) at fa8c06f82, and tools/_gamedir.py is on 1.4.x. EngineIdUniquenessTests is a standalone class: append it at the end of the test file (the diff context comes from later validator commits).
  - prereqs: fa8c06f82
  - conflicts: tools/tests/test_validate_xml_schemas.py
  - issues: 974
- `c1ce78207` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - schema check names project.mbproj loads
  - why: Schema-gate follow-up that classifies project.mbproj-named files and exits 1 on real faults in named-file runs; the gate is absent on 1.4.x, but the engine loaders it models (XmlResource.GetMbprojxmls, MBObjectManager.GetMergedXmlForNative, HandleXsltList, LoadXmlWithValidation, ModuleHelper.GetXsdPathForModules) all exist in the v1.4.8 dump.
  - port notes: Port in validator order after fa8c06f82 and 48d66b59a (1.5.x history: fa8c06f82, 332f2ac71, 48d66b59a, 4644bf493, this, then bc30b76da, 09f3126a7, f1675c058). Take tools/validate_xml_schemas.py, tools/tests/test_validate_xml_schemas.py, tools/README.md, .claude/rules/moduledata-validation.md, docs/features/moduledata-validation.md, docs/modding/validation-and-testing.md and the two lessons; .claude/skills/deep-review/lenses/7-xml.md exists only once fa8c06f82's lenses land. Leave the edits to 1.5.x-only RCAs (rca-generated-lord-kits, rca-hidden-orders, rca-mouth-of-sauron-gear); rca-schema-check is optional. The docstring cites v1.5.3: method presence in v1.4.8 is verified, behaviour is not, so re-check the modeled resolution against the v1.4.8 dump. audit_mbproj_registration.py, which it defers to, is already on 1.4.x.
  - prereqs: fa8c06f82, 48d66b59a
  - conflicts: tools/tests/test_validate_xml_schemas.py, tools/validate_xml_schemas.py
  - issues: 980, 983

### group `issue-983-mbproj-audit`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .gitattributes, tools/lint_docs.py, tools/test_hooks.sh, tools/tests/test_validate_xml_schemas.py, tools/validate_xml_schemas.py
- `bc30b76da` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - mbproj audit by type, RCA follow-up gate
  - why: Rewrites the project.mbproj registration audit (unchanged on 1.4.x since the fork, with DEAD-ID meaning a soln_* id outside the vanilla vocabulary) to read the file as the engine does and flag startup-throw shapes and rows neither engine loader reads, the class behind the 2026-06 spider CreateAgent crash, and adds an RCA FOLLOW-UP gate to lint_docs.py and its commit hook.
  - port notes: Take tools/audit_mbproj_registration.py and the new tools/tests/test_audit_mbproj_registration.py, re-deriving the engine model for v1.4.8: the managed half holds (exact-id match and absent-file tuple in MBObjectManager.GetMergedXmlForNative at C:\Decompiled_Bannerlord\_categories_v1.4.8\Core\TaleWorlds.ObjectSystem\TaleWorlds.ObjectSystem\MBObjectManager.cs:920-945; Module.cs has 9 GetMergedXmlForNative requester call sites; MBMusicManager.cs:345 reads soln_soundtrack), but NATIVE_BY_TYPE (thirteen types from v1.5.5 native disassembly, FUN_180719810) is UNVERIFIED for the v1.4.8 native DLL. The script imports _parse_like_engine and mbproj_rows from tools/validate_xml_schemas.py, absent on 1.4.x (created on 1.5.x by fa8c06f82): port that first or inline both helpers. Re-snapshot docs/reference/lotrlome-armory-snapshot/project.mbproj from the 1.4.8 install (plus the .gitattributes eol line) instead of copying 1.5.x's. The lint_docs.py FOLLOW-UP check, --staged mode and check-doc-config-drift.sh change are in scope but will conflict: 1.5.x lint_docs.py moved through d6b537092, 9c310bc23, 719074072, 30abf550b and c79a58521 since the fork, 1.4.x's not at all. Follow-up 09f3126a7 (exact mbproj ids) should travel with this; 12b010626's touch is the v1.5.6 bump. Leave .claude/skills/improve/references/run-protocol.md (absent on 1.4.x). The modding-doc corrections (native reads a row's type) depend on the native check.
  - prereqs: fa8c06f82
  - conflicts: .gitattributes, tools/lint_docs.py, tools/test_hooks.sh, tools/tests/test_validate_xml_schemas.py, tools/validate_xml_schemas.py
  - issues: 983, 984, 987

### group `issue-729-item-slot-rejected`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
- `2420508fb` 2026-10-30 PORT P2 pick=conflict :: fix(tools): v2.0.36 - refuse items written to a slot they cannot fill
  - why: Adds the ITEM_SLOT_REJECTED validator error for equipment rows the engine refuses, a data gate whose engine claim holds on v1.4.8 (Equipment.DeserializeNode only assigns when IsItemFitsToSlot passes, else Debug.FailedAssert, Equipment.cs:214-220).
  - port notes: Take slot_rejection_issues and its tests in tools/tests/test_validate_moduledata.py, the ITEM_SLOT_REJECTED --code in .claude/hooks/check-moduledata-validation.sh, the rule and doc lines, and the apply_isengard_troop_revamp.py Cape-to-Body fix. On 1.4.x that script still writes Cape at lines 276 and 283, even though troops_isengard.xml already has Body. The pass imports tools/audit_battle_equipment_memory.py from 3dac7ab5b; if that is not ported, extract its slot rule instead. The 1.4.x validate_moduledata.py is an older shape: wire the call into its `if game_modules.exists():` branch next to generator_item_ref_issues. The 1.4.x hook code list lacks the other 1.5.x codes, so expect conflicts there.
  - prereqs: 3dac7ab5b
  - conflicts: tools/tests/test_validate_moduledata.py, tools/validate_moduledata.py
  - issues: 729

### group `tests-architecture-gates`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Core/NotableTemplateGenderTests.cs, TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs
- `8caac7da5` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: test(gates): v2.0.36 - gates for entry points, services and sprites
  - why: It adds unit-test gates for thin entry points, service signatures, service locators, prefab sprites and the #371 TAOM/Dependencies version pairing; the rules are the same on 1.4.x, but the committed baselines describe 1.5.x code.
  - port notes: Take:
- DependenciesPairing_MainMetadataEqualsDependenciesVersion. Merge by hand, since the 1.4.x BundledDependencyManifestTests.cs differs. It passes today: both SubModule.xml files pin v2.0.6 on origin/bannerlord-1.4.5.
- ServiceLocatorGateTests. A grep of the 1.4.x tip found IoC.Resolve only inside comments of in-scope files, so it should start at zero.
- PrefabSpriteTests.
- The entry-point and service-signature gates, with EntryPointLineCountBaseline.txt and ServiceSignatureBaseline.txt regenerated from the 1.4.x tree (the 18 and 113 rows are 1.5.x).
Adapt: 1.4.x lacks the RepoPaths helper, PrefabExtensionBrushTests.GameDir (2c7f7fae9) and the Client/ folder the scans include. Leave the NotableTemplateGenderTests hunk: the crash it guards is in v1.5.0's Advanced Starting Options Trader start. None of this could be run in this audit, which forbids dotnet test.
  - conflicts: TAOM.Tests/Core/NotableTemplateGenderTests.cs, TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs
  - issues: 371

### group `tools-data-gates-validator-codes`  P2  track=data-integrity  commits=1  sim: clean=0 conflict=1
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/audit_armory_refs.py, tools/audit_fbx_lods.py, tools/generate_career_kits.py, tools/taom_schema.py, tools/tests/test_audit_armory_refs.py, tools/tests/test_audit_fbx_lods.py, tools/tests/test_generate_career_kits.py, tools/tests/test_generator_regions_in_sync.py, tools/tests/test_rebuild_name_tags.py, tools/tests/test_validate_moduledata.py, tools/tests/test_validate_xml_schemas.py, tools/validate_moduledata.py, tools/validate_xml_schemas.py
- `f1675c058` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.36 - validator codes and one command for data gates
  - why: It adds tools/data_gates.py and validator codes, among them TROOP_ID_REMOVED (a removed troop id leaves a hollow character in saves) and TOWNSFOLK_WITHOUT_BATTLE_ROSTER, whose engine premise holds on v1.4.8, but much of the commit relies on validators and tools only 1.5.x has.
  - port notes: Take:
- TROOP_ID_REMOVED with a 1.4.x baseline: regenerate tools/shipped_troop_ids.txt from the v2.0.28 troop files instead of taking the 945 v2.0.36 ids. c5b84fb41, origin/fix/public-armory-ids-145 and origin/bannerlord-1.4.5 each define the same 857 troop ids, so the gate starts clean.
- TOWNSFOLK_WITHOUT_BATTLE_ROSTER. In v1.4.8, BasicCharacterObject.cs:108 makes FirstBattleEquipment null for a civilian-only character, as the commit claims for v1.5.6.
- INCONSISTENT_WEAPON_SLOT, apply_culture_skills_traits.py --check, audit_mount_parity.py --strict and translate_with_claude.py --module.
Adapt data_gates.py to the gates 1.4.x has. TROOP_ID_REMOVED, SKILL_TEMPLATE_MISMATCH, ARMOUR_ACQUISITION_REF, CREATURE_GEAR_OBTAINABLE, BROKEN_FACTION_REF, SCHEMA_INVALID, MISSING_COLLISION_BODY and ITEM_SLOT_REJECTED appear in no tools/*.py on the 1.4.x tip. Leave the fords_of_war schema glob, generate_career_kits.py (925db38d1), audit_armory_refs.py and audit_fbx_lods.py unless the commits that introduced them are ported.
  - conflicts: tools/audit_armory_refs.py, tools/audit_fbx_lods.py, tools/generate_career_kits.py, tools/taom_schema.py, tools/tests/test_audit_armory_refs.py, tools/tests/test_audit_fbx_lods.py, tools/tests/test_generate_career_kits.py, tools/tests/test_generator_regions_in_sync.py, tools/tests/test_rebuild_name_tags.py, tools/tests/test_validate_moduledata.py, tools/tests/test_validate_xml_schemas.py, tools/validate_moduledata.py, tools/validate_xml_schemas.py
  - issues: 295

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 3, batch 11, batch 12, batch 19, batch 21, batch 22, batch 24, batch 25, batch 31, batch 34
