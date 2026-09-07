# Lessons — Data, Content & Cultures

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new Data, Content & Cultures lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### Every new main culture needs the 6 stage_2 education templates, or age-8 children CTD
The v1.4.7 engine resolves `child_education_templates_stage_2_page_0_branch_{0-5}_{culture.StringId}` at the Year8 education stage and dereferences the looked-up `CharacterObject` with **no null guard** (`EducationCampaignBehavior.GetSpecialCharacterPropertiesForOption` → `.Equipment`). A `is_main_culture="true"` culture with no templates is a guaranteed CTD the moment any child of that culture turns 8 — invisible until then because ages 2/5 never consult these ids, and the notification itself validates fine (the NRE fires only when the education screen builds its option previews on click). Four cultures shipped with the gap: lothlorien (reported, crash bundle `94c7b795`), umbar, goblin, mistymountainorcs.
- **Why missed:** the education-template requirement lived only in the `kingdom-creation.md` File 8 checklist, which postdates lothlorien/umbar; and #267 fixed the orc cultures' education *equipment* rosters (the cosmetic, null-safe half) without the *character* templates (the crashing half) — a half-fix that made the gap look covered on grep. No validator asserted the culture→template contract, so nothing failed at authoring time. Bonus obfuscation: PatchShield's finalizer unwrapped the `TargetInvocationException` and rethrew the bare inner NRE, resetting its stack to `ViewModel.ExecuteCommand_Patch3` — the crash bundle pointed at UI plumbing, not education data.
- **Prevent:** `tools/validate_moduledata.py` now ERRORs (`MISSING_EDUCATION_TEMPLATES`) for any main culture missing any of the 6 (derives the culture set from `taom_spcultures.xml`, never a hardcoded list — same shape as the PrisonerRecruitment lesson above); pre-commit hook runs it. PatchShield finalizers now rethrow the ORIGINAL exception so the inner chain (and its intact stack) reaches the crash reporter. When fixing a "missing per-culture data" crash, always enumerate ALL cultures against the contract before scoping the fix to the reported one.
- **Source:** docs/reviews/rca-education-crash-fix-2026-07-21.md (issue #354)

### A safety barrier that rests on shipped data needs a test, not a doc paragraph
When you establish that a feature is safe because of a fact about the shipped data ("no troop pairs X with Y", "no bandit culture is classified onto a side", "every id in this map resolves"), you have verified a *coincidence with good hygiene*, not an invariant. It is true until the next content author edits that file, and nothing — no schema, no type, no validator — will say otherwise. Write the assertion as a test that DERIVES its id set from the authoritative data file rather than hardcoding it, so new content is covered automatically. The distinction to apply: "is this true?" (verified once, decays) vs "what enforces this?" (holds).
- **Why missed:** PrisonerRecruitment (2026-07-16). The −2-bandit-cost guarantee rested on three barriers. Barrier 2 (no bandit culture appears in `alignment.json`) got a test *because it was an editable JSON config file*. Barrier 1 (no `occupation="Bandit"` troop carries a mainline culture) got only a doc paragraph — despite being equally editable troop XML. The author held the rule for one file type and not the other. Vanilla keys the −2 on per-troop `Occupation` but gates recruitability on per-culture `Culture.IsBandit`; those are independent, so a troop pairing `occupation="Bandit"` with a mainline culture would be both recruitable AND waivable, silently zeroing a −2. No such troop exists — nothing but the new test stops one being authored. Caught by the data-flow agent, missed by the other 4 and by the author.
- **Prevent:** for every "this is safe because the data says so" claim, ask what edit would falsify it and whether anything fails when someone makes that edit. If the answer is "nothing," that claim is a test you haven't written yet. Derive the id set from the data file (`taom_spcultures.xml` `is_bandit="true"`), never hardcode — a hardcoded list goes stale exactly when new content makes the test matter most. Include a floor assertion (`Assert.IsTrue(scanned >= 8)`) so a regex that silently matches nothing fails loudly instead of vacuously passing. Same shape as the BannerBearers dead-key CRITICAL (2026-07-16): silent at every layer, so only a resolution test defends it.
- **Source:** docs/reviews/rca-prisoner-recruitment-2026-07-16.md findings 1-2

### Porting a classification with a CHANGED lookup key must re-audit the inputs the new key drops
When a port re-keys a classification (donor keyed on culture, port keys on kingdom StringId via a data table; or any A→B key swap), the new key is usually correct for the SHIPPED set but silently loses every input the old key covered that isn't in the new table. Audit the inverse: enumerate what resolves to the DEFAULT / catch-all bucket under the new key, and confirm that set matches the donor's. A table keyed on shipped ids does not cover dynamically-created entities (player-founded kingdoms, revolt kingdoms, runtime-spawned objects).
- **Why missed:** WotR-momentum (#327, 2026-07-03). Enrollment swapped LOTRAOM's culture-based siding (`IsGoodCulture`) for kingdom-StringId siding via `alignment.json`. Correct for all shipped kingdoms (the table covers them), but a player-founded kingdom (`new_kingdom*`) isn't in the table → resolves Neutral → never enrolls → the player's own war contributions aren't credited and their kingdom never appears on the meter. All 5 deep-review agents + the author checked the shipped-kingdom set (which the table covers fully) and never asked "what ids are NOT in the table." Codex caught it by tracing `KingdomManager.new_kingdom` id creation.
- **Prevent:** when a port changes what a classification is keyed on, add a fallback for the default bucket that reproduces the donor's key (here: `GetKingdomSide(id)` Neutral → fall back to `GetCultureSide(kingdom.Culture.StringId)`), and add a test for a dynamically-created entity (`SweepEnrollment_PlayerFoundedKingdom_EnrollsByCulture`). Distinct from the faithful-port *behavior* gap (donor code inherited verbatim) — this is a deliberately CHANGED mechanism whose new blind spot went un-audited.
- **Source:** docs/reviews/rca-wotr-momentum-2026-07-03.md finding C1

### A code-side filter on an engine enum must be proven against TAOM's shipped data, not vanilla's
The mirror of the lesson below. When code filters entities by an engine enum (`Occupation.Bandit`, `Occupation.Mercenary`, an item category, a `DefaultGroup`), the value's *meaning* comes from the engine but its *population* comes from TAOM's XML — and TAOM routinely diverges from vanilla there. `DeliverPersonnelLotrIssueQuest` counted prisoners with `Occupation.Bandit`, exactly as its vanilla ancestor does. TAOM declares that occupation on **8 troops in the whole mod**, all hideout bosses: `spclans.xslt` deletes five vanilla bandit clans (`sea_raiders` included), and the eight `is_bandit="true"` LOTR cultures that replace them point `bandit_bandit`/`bandit_raider`/`bandit_chief` at ordinary faction troops (`dunland_peasant` is `occupation="Soldier"`, `culture="Culture.empire"`) because those are the same entries Dunland/Rhûn/Harad recruit from. Only vanilla `looter`s ever matched, so both `DeliverPersonnel` quests were uncompletable for anyone not farming looters. Neither side was wrong alone: the content decision was correct, the predicate was correct for vanilla, and they were authored months apart in different features.
- **Why missed:** the assumption was **inherited, not authored** — the feature doc's design survey recorded the *vanilla* issue's dependency ("Player bandit-occupation troops", row 33) and the port preserved it as a requirement instead of re-validating it. Compounding it, the predicate sat in the quest shell, which ADR-008 exempts from test coverage, so no test could have failed. A wrong rule in a tested layer fails a test; a right rule in an untested layer still works; the two together shipped past 4500 tests, five review agents and a Codex pass. Deep-review's Data Flow agent traces XML→C# in the forward direction (config nothing reads) — this is the reverse direction (a C# constant whose data almost never exists) and was not in its brief.
- **Prevent:** before shipping any predicate that filters on an engine enum, run the grep that proves the population — `grep -ro 'occupation="Bandit"' Main/_Module/ModuleData/` would have returned 8 and ended the discussion in one command. If the predicate decides whether a quest/feature can COMPLETE, it does not belong in an entry point: move it to the service and test it, or it is untested by policy. When porting a vanilla mechanic, treat every dependency the design survey records as a **question to re-ask against TAOM data**, not as a specification.
- **Source:** docs/reviews/rca-lotr-issues-deliver-personnel-2026-07-30.md (#368)

### An authored data value that names a code-side enum needs a consumer gate
When content (XML, JSON) carries a value whose meaning is a code-side enum (`PassiveEffectType`, an effect kind, a mechanic id) and a service silently *caches/parses* it, an enum value with **no runtime consumer** ships as an invisible no-op: no crash, no warning, no failing test — indistinguishable from "the player didn't pick it." TAOM shipped six `PassiveEffectType` values across ~211 career pips (~16% of all pip-passives) that nothing read; the 2026-05-29 wrapper-schema fix made it *worse* by activating more dead pips into the cache without surfacing that they went nowhere.
- **Why missed:** content and consumers evolved on separate tracks with no cross-check. A parsed-and-cached value *feels* wired; only a player noticing a weak build surfaced it. A feature-doc note even listed five of the six as a "known limitation" — known but un-gated.
- **Prevent:** maintain a compiled source-of-truth set of consumed enum values (`PassiveEffectConsumers`), warn at config load for any authored value not in it (`CareerConfigProvider.ValidatePassiveConsumers`), and add a regression test that loads the REAL shipped data and asserts every used value has a consumer (`CareerChoicesIntegrationTests`). A "known limitation" in a doc is not a gate — encode it as a test or a load warning or it ships a third time.
- **Source:** docs/reviews/rca-career-phantom-passives-2026-06-26.md

### A data re-tune that changes a displayed value must also update the displayed text
A magnitude/value-only data migration leaves any player-facing description that *embeds* that value stating the old number — and if the consumer's semantics also changed (flat count → multiplicative %), the text is now wrong on units, not just magnitude. The career re-tune changed ~186 pip magnitudes to a uniform 10–15% band but left descriptions reading "+5% stealth" (now 10%) and "+3 ammo" (now +10% multiplicative). `CareerChoiceObjectVM.Description` renders the string verbatim, so the lie is shown in-screen.
- **Why missed:** the re-tune was scoped to "change the number that's *applied*"; it didn't model that the same number is *also displayed* from a second source (the `description=` string + its `{=key}` loc entry). One value, two surfaces.
- **Prevent:** when a re-tune script edits a value that appears in a description, edit the description in the same pass — type-phrase-anchored so ability-mutation numbers aren't touched (`retune_phantom_descriptions.py`) — across BOTH the inline default and the loc-source strings, and flag the per-language re-translation. Better still, author descriptions WITHOUT the embedded number when a value is expected to be re-tuned. A localized string has N+1 surfaces (inline default + N language files); state the deferred ones explicitly.
- **Source:** docs/reviews/rca-career-phantom-passives-2026-06-26.md

### A downward rebaseline delta often means the culture MODIFIER is wrong, not the troops
When normalizing troops onto a baseline + per-culture-modifier curve, troops that move DOWN (actual > formula) are not automatically "over-tuned troops to be nerfed." They can equally mean the culture's *modifier* is too weak for its intended identity, and the author expressed that identity by hand-authoring individual troops above the curve. The 2026-06-24 rebaseline found Dol Guldur's `dg_uruk_*` line authored at 935 (L26) — above even Isengard's 800 — while the `dolguldur` modifier was near-neutral (net −5). Blindly normalizing would have nerfed Sauron's lore-elite uruks below Isengard. The fix was to bump the dolguldur modifier to elite (~Isengard tier), keeping the uruks strong VIA the modifier (the correct mechanism) rather than as off-curve individual troops.
- **Why missed:** "apply the existing curve to everything" frames the curve as ground truth, so the instinct is to pull off-curve troops onto it. But the curve is baseline + modifier, and a faction whose troops are systematically above its modifier line is evidence the *modifier* understates the faction — surfaced only by inspecting the downward deltas per-culture before applying.
- **Prevent:** before an `--apply`, review the downward changes grouped by culture (not just the headline outlier count). A whole faction's elite line dropping together → interrogate that culture's modifier against its lore/role before normalizing. Upward corrections (trash-tier elites → curve) are almost always right; downward corrections of a *coherent faction tier* deserve a "is the modifier wrong?" check. Surface it to the owner as a design decision, don't silently nerf.
- **Source:** docs/features/troop-skill-balance.md (2026-06-24 rebaseline — Dol Guldur modifier bump)

### An elite sub-line inside a culture's troop file needs id-based modifier routing
`rebalance_troops.py` assigns the culture modifier by FILENAME, so every troop in `troops_mordor.xml` gets the (weak) `mordor` modifier — including the elite Black Uruks (`mordor_uruk_*`), which are far better than the orc rabble and got dragged onto the orc floor. The fix is the existing `iron_hills` pattern: an id-prefix rule in `detect_culture` routes the sub-line to its own modifier (`mordor_uruk_*`→`mordor_uruk`, a +52 elite tier between Gundabad and Dol Guldur). A flat per-file modifier cannot express "weak rabble + elite line" in one file — only id-routing can.
- **Why missed:** the first rebaseline treated one file = one culture = one modifier, which is true for most files but not for any file that mixes a rabble tier and an elite tier (mordor: orcs + Black Uruks; erebor: dwarves + iron_hills nobles). The Black Uruks were normalized down silently because the weak `mordor` modifier applied to them with no error.
- **Prevent:** when a culture's file contains a clearly-elite named sub-line (Black Uruks, Iron Hills nobles, a noble/guard branch), check whether it needs its own `detect_culture` id-route + `CULTURAL_MODS` entry rather than inheriting the file's base modifier. Symptom to watch in the balance overview: a sub-line whose names read "elite" sitting at the same parity-matrix cell as the culture's rabble.
- **Source:** docs/features/troop-skill-balance.md (2026-06-25 — Black Uruk `mordor_uruk` routing)

### Partial troop skill-blocks are intentional + the mixed-BOM repo is correctly handled — don't "fix" either
Two `rebalance_troops.py` behaviors LOOK like bugs to a mechanical-tooling review but are correct; a deep review will re-flag them unless told otherwise. (1) Many troops have PARTIAL `<skills>` blocks (an archer has Athletics/OneHanded/Bow/Throwing but no Polearm/TwoHanded) — this is role-appropriate authoring. `apply_skills_via_regex` updates only the *present* skills onto the curve and leaves role-irrelevant skills absent. Adding all 8 skills would be a DESIGN CHANGE (giving archers melee stats), not a fix. (2) The `troops_*.xml` set is MIXED-BOM (some files have a UTF-8 BOM, most don't). The tool's plain `encoding='utf-8'` read+write preserves each file's BOM state correctly (BOM→U+FEFF-as-data→BOM; no-BOM→no-BOM). Switching to `utf-8-sig` write (the usual "fix") would ADD BOMs to the non-BOM files — a regression.
- **Why missed:** a mechanical-tooling agent reasons about what the code does ("missing skills are never added", "BOM survives only by accident") and flags both as data-loss/fragility. The domain meaning (archers don't need polearms) and the repo's mixed-BOM reality make both correct. Deep review 2026-06-25 flagged partial-block HIGH + BOM MED; the balance agent + direct verification refuted both.
- **Prevent:** when reviewing `rebalance_troops.py`, treat partial skill-blocks and the utf-8 (not utf-8-sig) read/write as INTENDED — verify the present skills are on-curve rather than demanding all-8. A genuinely more robust BOM fix is the bytes-mode `had_bom` round-trip (`tools/README.md`), which preserves per-file state AND is string-preprocessing-safe — but it must NOT force a BOM, and it carries a CRLF-in-empty-block-insertion caveat. Not urgent; the current behavior ships correct data.
- **Source:** docs/reviews/rca-troop-skill-balance-2026-06-25.md (refuted partial-block HIGH + BOM MED)
- **SUPERSEDED 2026-08-31 (#522), both halves.** Half (1) was wrong about the engine.
  `CharacterObject.GetSkillValue` returns **0** for a skill the block never declares, so a partial
  block is not "leave this role-irrelevant skill alone", it is an explicit zero. On an upgrade
  target that reads as a stat wipe: `mordor_uruk_skirmisher -> mordor_uruk_crossbow` showed Bow
  130 -> 0 purely because the target omitted the element. The 2026-06-25 review was right that the
  tool could not repair them and wrong that it should not; all 8 are now declared in `troops/`, and
  the omission is gated. Half (2) was right that the plain utf-8 path happened to preserve the four
  BOM files, and is now moot: the writer uses the bytes round-trip this very lesson recommended,
  which also stops an LF-only file being rewritten wholesale as CRLF.

### Level monotonicity excludes militia by design — don't "fix" intentional toughness
A balance pass must keep stats monotonic by level (no lower-level troop out-stats a higher-level one), checked by `analyze_troop_balance.py` across upgrade paths and within culture+group. But TAOM militia deliberately take the **L21 baseline regardless of level** so they make sieges and village defense costly — so a L6/L11 militia out-statting mid-level regulars is intentional, and the monotonicity check **excludes militia** (`is_militia`). When a check surfaces a class of "violations" that are all one intentional design category, exclude the category and report the count — do not normalize it away.
- **Why missed:** the monotonicity request ("no lower level stronger than a higher level") reads as an absolute rule; applied literally it would have nerfed every culture's militia. The owner clarified militia are a deliberate defensive lever, not part of the clean progression.
- **Prevent:** before "fixing" a wholesale inversion class, split the violations by category (militia vs professional). 79/79 here were militia; 0 were professional. A monotonicity/parity checker should carry a documented exclusion list (militia, creatures, off-grid) so intentional outliers don't generate noise or get auto-corrected. Also use TOTAL-skill (not per-skill primary) for the comparison — weapon-spec swaps preserve total, so a melee→archer upgrade isn't a false "primary skill dropped" inversion.
- **Source:** docs/features/troop-skill-balance.md (2026-06-25 — monotonicity check + militia carve-out)
- **AMENDED 2026-08-31 (#522).** The category was right; the exclusion was drawn one level too
  wide. Excluding militia TROOPS also excluded every edge that leaves a militia, and the worst
  upgrade edge in the game was sitting inside that blind spot for two months while the check
  printed "0 inversions". The exemption is now per-EDGE: militia-to-militia is flat by design and
  exempt, a militia feeding a real line is checked like anything else. The within-culture level
  sweep still excludes militia entirely, which is correct and is now disclosed in the report
  instead of implied. Generalisation worth carrying: **exempt the edge, not the entity**, and when
  you add an exemption ask what a bug hiding inside it would look like.

### Enumerate new-attribute rows from the upstream source-of-truth, not the existing config
When extending a config file with a new attribute, enumerate the expected rows from the **upstream source-of-truth** (e.g. `Main/_Module/ModuleData/charactercreation/cultures.json` for CC-selectable cultures, `taom_spkingdoms.xml` for kingdoms, `Languages/` for translation files), not from the config's own existing row list — the existing rows reflect what someone happened to add before, so copying them inherits the gaps.
- **Why missed:** Issue #110 (LOTRAOM `StartingEquipmentGold` port, 2026-05-06): added a `playerGold` attribute to the 15 existing `<Culture>` rows of `startup_resources_config.xml`; `empire` (Dunland) was missing — caught by Claude `/deep-review`; `shaghana`/`abanissa` were missing — caught by Codex. Players picking those cultures and the 17 Shaghana/Abanissa NPC lords silently started with 0 gold. `cultures.json` was available but not grepped.
- **Prevent:** Identify the upstream source-of-truth first, enumerate from it, verify every entry has a row, add missing rows. Cross-reference checklist for any culture-keyed config: `cultures.json`, `taom_spkingdoms.xml`, `taom_spcultures.xml`, `clans.xml` + `lords.xml`. Don't rely on "missing means default" unless loader semantics explicitly handle it. Build-time prevention candidate: a test cross-referencing `cultures.json` cultures against the config's rows.
- **Source:** memory/feedback_enumerate_from_source_of_truth.md

### Classify unfamiliar TAOM IDs by exhaustive grep, not by assumption — and load relevant memory FIRST
When you encounter an unfamiliar TAOM ID and need to classify it (culture? kingdom? clan? settlement? troop?), grep exhaustively across kingdom/clan/lord/culture XML AND the project memory directory before concluding. A single source can show an ID exists for one purpose without ruling out others.
- **Why missed:** Issue #110 (2026-05-06): read that `shaghana`/`abanissa` were declared in `cultures.json` with starting settlements `town_A6`/`town_A14`, concluded "Aserai-region custom cultures, no NPC clans, player-only," set `gold="0" influence="0"`. All wrong — they are full independent kingdoms in `taom_spkingdoms.xml` (rulers Taskral/Châjaphân) with 17 NPC lords combined (Shaghana 9 + Abanissa 8) in `lords.xml`, so every NPC lord started broke. `kingdom-culture-mapping.md` already stated they were independent kingdoms (line 58), but it wasn't loaded before classifying.
- **Prevent:** Mandatory grep checklist for any unfamiliar ID — (1) memory dir FIRST, (2) `taom_spkingdoms.xml`, (3) `taom_spcultures.xml`+`spcultures.xslt`, (4) `clans.xml`, (5) `lords.xml` lord count, (6) `cultures.json`, (7) `cat kingdom-culture-mapping.md`. Anti-pattern signals to stop and grep on: "plausibly an X," "settlement prefix is Y so Y-region," "looks like no NPC clans," "may be intentional," "probably a sub-culture of Z" — each is a guess phrased as a conclusion. Note: Gondor kingdom = `empire_w`, Gondor culture = `gondor` (kingdom ID ≠ culture ID).
- **Source:** memory/feedback_classify_by_grep_not_by_assumption.md + kingdom-culture-mapping.md

### Verify every referenced troop/item/clan ID against the canonical XML before commit — sibling-naming-symmetry is a false signal
When converting a user's prose spec into a config (recruitment pool, party-template, any troop reference), bridge the gap with grep, not pattern-matching. If `wain_youngblood` and `wain_glaiveman` exist, that does NOT imply `wain_cavalry` exists.
- **Why missed:** RCA `docs/reviews/rca-rhun-gondor-recruitment-2026-05-23.md` — authored `("wain_cavalry", 2)` from the user's "Wain Cavalry - 2" because siblings looked symmetric. The actual troops are `wainrider_horseman`/`wainrider_cavalry` (different prefix). The bogus ID silently returned null from `MBObjectManager.GetObject<CharacterObject>(...)`, dropping 20% of recruitment rolls at 4 settlements. The test passed because it asserted the service's return value, not troop existence. Caught by `/deep-review` Agent 5 (Data Flow).
- **Prevent:** Before committing any C#/XML/JSON referencing a troop/item/settlement/clan/kingdom/culture/character ID, grep the canonical source-of-truth for the EXACT id (troops → `troops/troops_<culture>.xml`; items → `LOTRLOME_items/<culture>/` or `LOTRAOM_weapons.xml`; clans/kingdoms → `taom_spclans.xml`/`taom_spkingdoms.xml`). Zero matches = wrong, fix before commit (don't intuit — read the surrounding context to find the right name). Multiple plausible IDs = state the ambiguity to the user. Service-return-value tests don't validate cross-file ID existence — that's the job of `/deep-review` Agent 5, `tools/validate_all_troop_refs.py`, or pre-commit greps.
- **Source:** memory/feedback_verify_troop_ids_against_canonical_xml.md + docs/reviews/rca-rhun-gondor-recruitment-2026-05-23.md

### LOTRLOME armor items: multiple subfolders per culture — grep ALL subfolders for the prefix before choosing a generator output folder
When LOTRLOME_Armory has multiple subfolders for one culture (e.g. `erebor/` + `iron_hills/`), every generator script MUST grep ALL `LOTRLOME_items/*/` subfolders for the item prefix before defaulting the output folder. The first folder already containing items with that prefix is the canonical home; adding the same id to a different folder causes silent engine shadowing (one entry overrides the other, warning logged, no crash).
- **Why missed:** RCA `docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md` (issue #211, deep-review Agent 5): `generate_erebor_armor.py` authored 123 `sk_dwarf_iron_*` items to `erebor/`, but 118 of those IDs already existed in `iron_hills/`. The KEYforce spec was named `erebor_armors_and_troops.txt` but defined Iron Hills items whose canonical home is `iron_hills/`. Caught by the data-flow agent tracing mesh → item → troop; fixed by rolling back `erebor/` and re-running with `DEFAULT_ARMORY_BASE` set to `iron_hills/`.
- **Prevent:** Before authoring a generator, `grep -l 'id="<prefix>'` across `LOTRLOME_items/*/*.xml`. If any folder has the prefix, default there. `tools/validate_all_troop_refs.py` now uses recursive glob over `LOTRLOME_items/**/*.xml` and catches `ar_*` refs; a future cross-file duplicate-id check would catch this at validation time. CLAUDE.md "Equipment & Armory" has a per-prefix canonical-folder table — consult it.
- **Source:** memory/feedback_multi_folder_id_uniqueness.md + docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md

### LOTRLOME armor needs `covers_*` attributes or the mesh equips invisibly
When authoring or duplicating LOTRLOME_Armory items, the `<Armor>` element's `covers_*` attribute is required for the mesh to render: leg items need `covers_legs="true"`, glove/arm items need `covers_hands="true"`, body items need `covers_body="true"` (plus optional `covers_legs`/`covers_hands` for long robes / full gauntlets), head items need `hair_cover_type`+`beard_cover_type`. Without them the engine equips the item but renders bare legs/hands.
- **Why missed:** RCA `docs/reviews/rca-career-starting-equipment-2026-05-19.md` — authored 15 starter armor items, preserved `covers_body="true"` but missed `covers_legs`/`covers_hands`. In-game the player wore head+body+cape correctly but appeared bare-legged and bare-handed despite the items being equipped per the inventory UI. The attributes are universal in the source data; they just weren't copied. The 2026-05-19 review caught a missing-IDs case but not the missing-attribute case because no agent's prompt cross-referenced attribute completeness against the source schema.
- **Prevent:** Copy the FULL `<Armor>` element verbatim when duplicating; don't strip "optional"-looking attributes. Sanity check: `grep -E '<Armor leg_armor=' <culture>/leg_armors.xml | grep -cv 'covers_legs="true"'` should return 0 (same for arm items + `covers_hands`). Extend `/deep-review` Agent 2 to verify per-slot cover attributes against the source schema. Same trap class applies to weapons (`weapon_class`/`thrust_speed`/`swing_damage`).
- **Source:** memory/feedback_lotrlome_armor_cover_attributes.md + docs/reviews/rca-career-starting-equipment-2026-05-19.md

### Custom-skeleton-race NPCs need race-rigged armor — the race attribute and equipment meshes are independent knobs
Declaring `race="dwarf"` (or any custom-skeleton race) on an NPCCharacter gives it the custom monster skeleton but does NOT change its equipment. Vanilla Bannerlord cloth/armor is rigged to the human skeleton and clips/floats on a custom skeleton. Only LOTRLOME_Armory items authored for that skeleton fit (`sk_dwarf_erebor_*`/`sk_dwarf_iron_*` armor, `sm_dwarf_erebor_*` weapons). Same applies to elf, orc, goblin, etc.
- **Why missed:** Issue #261, 2026-06-01 — the 12 Erebor wanderers shipped in vanilla green `tunic_with_shoulder_pads` that clipped the dwarf skeleton in Encyclopedia, town walk-about, and battle. The shared roster was never updated to dwarf items when the wanderers were authored, even though notables/companions/troops already had been.
- **Prevent:** Wanderer equipment lives in the shared roster `npc_companion_equipment_template_<culture>` in `equipmentsets/taom_wanderer_equipment.xml` (NOT inline in `taom_wanderers.xml`) — one roster edit fixes all wanderers of that culture. Fast audit: negative-lookahead grep `id="Item\.(?!sk_dwarf_|sm_dwarf_)` over the file — zero matches = clean (swap prefixes per race). Match template structure against `npc_companion_equipment_template_<vanillaculture>` in `SandBoxCore/ModuleData/sandboxcore_equipment_sets.xml` before assuming it's broken. Run `tools/validate_moduledata.py` after edits.
- **Source:** memory/feedback_dwarf_race_npc_needs_dwarf_skeleton_armor.md

### Townsfolk/notables need a battle `<EquipmentRoster>` or arena spectators spawn naked
Arena stand spectators are the settlement culture's own townsfolk/notables, spawned engine/scene-side with BATTLE equipment. Every TAOM culture shipped a civilian-only inline roster (`<EquipmentRoster civilian="true">`, no plain `<EquipmentRoster>`), so `FirstBattleEquipment` was empty → naked in every arena, every culture. The town walk uses civilian equipment → clothed (the asymmetry that makes it arena-only).
- **Why missed:** The arena audience spawn is engine/scene-driven with no TAOM behavior to patch, so the symptom only surfaced in-game; the `[MissionDiag][NakedSuspect]` dump showed `char='townsman_erebor' Body=empty Leg=empty`. Fixed by `tools/add_townsfolk_battle_rosters.py` (battle twin of each civilian roster; 1089 NPCs / 20 cultures, #295). Two dead-ends: `CharacterSpawner.InitWithCharacter` is the UI **tableau** spawner (encyclopedia/clan/CC previews), NOT the arena crowd (spectators are real `Mission.Agent`s); and the `as_human_warrior` action set on spectators is a red herring (their skeleton is the monster's, so race-rigged clothing binds once equipment is present — a prototyped `FaceMorphCompat`/`Patch52` GPU-morph guard was removed as unnecessary for the naked symptom).
- **Prevent:** When authoring a new townsfolk/notable NPCCharacter, add a plain `<EquipmentRoster>` mirroring the `civilian="true"` one, or re-run `tools/add_townsfolk_battle_rosters.py`. Rule note in `.claude/rules/xml-data.md`.
- **Source:** memory/feedback_townsfolk_need_battle_roster_for_arena_spectators.md

### A NEW item XML file only loads at process launch — validators/build/tests pass but the item is null in-game until a full restart
The transient/restart variant of the underwear bug: the item ref is CORRECT and `validate_moduledata.py` PASSES, yet the character is naked in-game because the item's NEW `*.xml` was authored AFTER the last game launch. Bannerlord loads managed item XML in two one-shot phases — **registration** at process launch (standard boot `Module.cs:246`→`267 LoadSubModules(loadNewModules:false)`→`1032 GetXmlListAndApply`, which stores each `<XmlName id="Items" path="...">` directory PATH STRING, `XmlResource.cs:142-182`, NOT a file list) and **glob+load** at campaign start (`Campaign.cs:1466 InitializeDefaultCampaignObjects`→`1471 LoadXML("Items")` → `MBObjectManager.cs:894` file-exists-else-`900/901/903` `new DirectoryInfo(path).GetFiles("*.xml")`, loads every `*.xml`). A directory registration loads even individually-unregistered files (why `gondor/starter_armors.xml` loaded from a directory-only registration) — but only files present at the campaign-start glob whose directory was registered at launch. Single `LoadXML("Items")` call site, no `FileSystemWatcher` in the campaign/object-system/mission trees → **nothing re-reads item XML mid-session; a full process RESTART is required for a new (or edited) item file to take effect.**
- **Why missed:** 2026-06-30 starter-equipment tuning (`tools/generate_starter_armor.py`) authored 12 new `starter_armors.xml` into already-registered `LOTRLOME_items/<culture>/` dirs. `validate_moduledata.py` (reads files off disk) PASSED, build + unit tests were green, and the work was reported "verified" — but none of those instantiate `MBObjectManager` or start a campaign, so all three are blind to engine load timing. Every non-Gondor character was naked after selecting a career; Gondor was fine only because its `starter_armors.xml` pre-existed the last launch (the file-existed-at-launch fact is the user's observed symptom; the decompile proves the load mechanism that makes it inevitable). A full restart fixed it. Mechanism decompile-verified + adversarially re-checked (workflow `naked-regression-prevention`, 2026-07-02).
- **Prevent:** A green validator/build/test run is necessary but NOT sufficient for ANY change that adds or edits item/equipment XML — the only proof is a full game **RESTART** + in-game visual check (new campaign, spawn/select the affected char, confirm clothed). Applies to the whole `generate_*_armor.py` family, `/new-culture`, `/author-armor`, and any new file dropped into a folder-registered `LOTRLOME_items/<culture>/` dir. The `generate_starter_armor.py`/`wire_career_starter_armor.py` scripts now print a RESTART-REQUIRED reminder after `--apply`, and `.claude/rules/moduledata-validation.md` carries the blind-spot caveat. Corollary trap: the glob is `GetFiles("*.xml")`, so a backup ending in `.xml` (e.g. `foo_backup.xml`) WOULD be globbed → duplicate item ids (engine silently shadows one); keep backups on a non-`.xml` extension (`.bak-*`).
- **Source:** docs/features/starting-equipment-tuning.md + workflow `naked-regression-prevention` (2026-07-02). Cites: Module.cs:246/267/1032, Campaign.cs:1466/1471/1520, MBObjectManager.cs:894/900/901/903, ModuleHelper.cs:232-234, XmlResource.cs:142-182.

### Notable NPCs need two-layer registration — the engine pools from the culture file, not the NPC file
A new notable lives in TWO coordinated files: the `<NPCCharacter id="spc_notable_{culture}_{N}" is_template="true" occupation="X">` element in `characters/npcs_{culture}.xml`, AND a `<template name="NPCCharacter.<id>" />` line in that culture's `<notable_templates>` block in `taom_spcultures.xml`. The engine's `HeroCreator.CreateNotable` pools ONLY from `<notable_templates>`; an NPC defined in (1) but not registered in (2) is unreachable — the engine reuses an existing template (producing clone notables with identical names/traits).
- **Why missed:** 2026-05-31 cultural-feats 3-pack (RCA `docs/reviews/rca-cultural-feats-3pack-2026-05-31.md`): added `spc_notable_{isengard,mordor,dolguldur,gundabad}_23` RuralNotable NPCs to feed new village-notable-count feats (target ceils 2 → 3) but didn't register them in `<notable_templates>`. The engine would have reused `_21`/`_22` for the 3rd slot — exactly the clone-notable bug the new templates were meant to prevent. Caught by deep-review Agent 5 (Data Flow). `validate_moduledata.py` flags `<template>` refs that don't resolve to an NPC, but NOT the opposite (an unreferenced NPC).
- **Prevent:** Add both the NPCCharacter and the `<template>` line in the same PR. Verify: `grep -c '<template name="NPCCharacter.spc_notable_{culture}_'` should equal the number of reachable `is_template="true"` notable NPCs for that culture. Same rule applies to Preachers and Headmen. `.claude/rules/xml-data.md` "Culture NPC Naming Convention" calls out the two-layer requirement.
- **Source:** memory/feedback_notable_template_two_layer_registration.md + docs/reviews/rca-cultural-feats-3pack-2026-05-31.md

### Hero skills come from `skill_template` (SkillSet), not from the `<skills>` block — editing `<skills>` is dead-code work
When a hero NPCCharacter (`is_hero="true"`) has both `skill_template="SkillSet.X"` AND an explicit `<skills>` block, the engine uses the SkillSet at hero generation and silently ignores the `<skills>` block. To change a hero's initial skills, edit the SkillSet the template points at, or swap the `skill_template` attribute. (NON-hero NPCs DO use their explicit `<skills>` block — the SkillSet mechanism is hero-specific.)
- **Why missed:** RCA TAOM lord-skills 2026-05-27 — wrote populated `<skills>` blocks into 738 adult lord NPCs across 17 cultures (commit `8665ca6`), but in-game Boromir showed OneHanded=145 not the explicit 295. Root cause: Boromir's vanilla `skill_template="SkillSet.spc_dandy_skills"` (OneHanded=140 in `SandBox/ModuleData/sandbox_skill_sets.xml`) survived all the XSLT/lords.xml edits because `apply_culture_skills_traits.py` only touched `<skills>`/`<Traits>`, not `skill_template`. Hero stats bake from the named SkillSet at creation. Fixed (commit `c5dc168`) by creating `Main/_Module/ModuleData/taom_lord_skill_sets.xml` with 120 TAOM SkillSets and repointing every adult lord's `skill_template` (Boromir → `SkillSet.taom_canonical_lord_1_75_skills`, OneHanded=295 → verified in-game at 302 with growth).
- **Prevent:** To change skill X of hero Y: find the current `skill_template`, edit that TAOM SkillSet's entry in `tools/apply_culture_skills_traits.py` (BASE_ARCHETYPES or per-NPC canonical), re-run `--all-cultures --apply`. Never hand-edit `<skill value=...>` inside a hero `<skills>` block expecting effect. Save-compat caveat: hero skills bake at creation — existing campaigns keep old values, only new campaigns + un-spawned heroes use updates. Architecture: `docs/ai-includes/lord-skills-authoring.md`, `docs/features/lord-skills.md`.
- **Source:** memory/feedback_skill_template_overrides_explicit_skills.md

### Hideout bandit-boss NPCs must be dedicated troops with `occupation="Bandit"` + the bandit culture
A bandit culture's `bandit_boss` must point at a dedicated `{culture}_boss` NPCCharacter mirroring the vanilla template (`SandBox/ModuleData/bandits.xml` `sea_raiders_boss`): `occupation="Bandit"` (value 15) + `culture="Culture.{bandit_culture}"`. Repoint BOTH the culture's `bandit_boss` (taom_spcultures.xml) AND the `{culture}_boss_party_template` 1× stack (taom_partyTemplates.xml). Never reuse a shared regular-roster troop.
- **Why missed:** RCA TAOM hideout boss "all friendly" fix, 2026-05-31 (CHANGELOG + `docs/features/bandit-management.md`). The vanilla hideout fight sets player/bandit teams non-enemy for the boss walk-up (`HideoutMissionController.OnInitialFadeOutOver` → `SetIsEnemyOf(..., false)`), restoring enmity only via the `bandit_hideout_start_defender` taunt dialog's consequence. But `GuardsCampaignBehavior.conversation_guard_start_on_condition` matches ANY conversation NPC with `Occupation == Soldier` (value 7) inside a settlement and shows "Can't talk right now. Got to keep my eye on things..." — so a boss with `occupation="Soldier"` had its conversation hijacked by the guard dialog, the taunt/fight options never appeared, enmity was never restored, all bandits stayed friendly, and the player was forced to retreat. The bandit culture is also required for `HideoutMissionController.SelectBossAgent` (`Culture.IsBandit && Culture.BanditBoss == character`).
- **Prevent:** When adding/revamping a bandit culture, CREATE a dedicated `{culture}_boss` troop — regular troops are shared (mid-upgrade-chain, regular party templates) and editing them in place corrupts the normal roster. Symptom signature: "boss fight, all units friendly" + the boss saying the guard's "keep my eye on things" line.
- **Source:** memory/feedback_bandit_boss_occupation_bandit.md

### A new PLAYABLE culture needs the 4 culture-keyed CC narrative menus + a cc_body_properties body
Making a culture playable (`charactercreation/cultures.json` entry) is not enough. It also needs entries in the 4 culture-keyed CC narrative menus — `charactercreation/{parents,youth,adulthood,education}_menu.json` — because `NarrativeMenuBuilder` shows an entry only when `entry.culture_id == selectedCulture.StringId` (or empty `culture_id`). A playable culture with zero entries renders the Family/Youth/Adulthood/Education stages BLANK. It also needs a `cc_body_properties.xml` `<Culture>` body (128-hex key) or the CC preview falls back to a default body (e.g. a human body on an orc culture). `childhood_menu.json` is culture-INDEPENDENT — needs no per-culture entries.
- **Why missed:** RCA issue #269 (2026-06-02) — goblin/mistymountainorcs were made playable but had no CC menu entries and no cc_body bodies, so the Family stage was blank; the clone pipeline (`insert_new_factions.py`) produced troops/equipment/wanderers but not CC narrative/body content. Fixed by `tools/insert_new_faction_cc_menus.py` (clones gundabad's 6 entries per menu with id/culture rename + Gundabad→culture display remap, textual append to preserve existing entries byte-for-byte) + manual cc_body `<Culture>` additions. Lindon (Culture.rivendell) + bluecraig (Culture.goblin) inherit their culture's CC content.
- **Prevent:** For a new playable culture, clone a sibling's entries in the 4 culture-keyed menus (rename `string_id`+`culture_id`, remap flavor text) + add a `<Culture>` to `cc_body_properties.xml`. CC menu `text`/`description` are plain inline English (not `{=key}`-localized) so no loc-key registration, but the source flavor must be remapped. Guarded by `ConfigIdValidationTests.NewCultures_HaveCharacterCreationMenuEntries`.
- **Source:** memory/feedback_new_culture_cc_narrative_and_body.md + docs/reviews/rca-new-factions-2026-06-02.md

### Cloning a faction/culture leaves player-facing DISPLAY text that a lowercase id-rename never touches
When a generator clones a TAOM faction/culture from an existing one (the new-factions scripts clone `goblin`/`mistymountainorcs` from `gundabad`), the clone copies two kinds of "gundabad": (a) lowercase technical ids (`Item.wm_gundabad_*`, `BodyProperty.fighter_gundabad`, `Culture.gundabad_raiders`, `SkillSet.spc_wanderer_gundabad`) preserved on purpose, and (b) capital-G / free-text player-facing DISPLAY words in `name=` attrs, culture `text=` descriptions, `<clan_names>` pools, notable names, and harvested `taom_module_strings.xml` strings. A case-sensitive lowercase `text.replace("gundabad", culture)` silently leaves every display string saying "Gundabad"/"Pale Uruk"/"pale orc".
- **Why missed:** RCA `docs/reviews/rca-new-factions-2026-06-02.md` (P0/C2/W2) — the goblin culture shipped named "Gundabad Orcs" with notables called "Gundabad Caravan Master"/"Cautious pale orc trader." Full extent: 2 culture names + 2 descriptions + 2 clan-pool names + ~24 loc strings + 36 notable names + 14×2 "pale orc" + 6 wrong-race-word ("orc" on a goblin). `/deep-review` (7 agents) checked structure/refs not free-text wording; Codex confirmed the fix; a completeness workflow still found the "pale orc"+"orc" siblings. This is a CLASS — every clone reintroduces it.
- **Prevent:** In the clone transform, after the id-rename, remap display WORDS culture-aware (bracketed tag `[Gundabad]`→`[Tag]`, race word, faction word) + BESPOKE phrase subs for lore-specific text (descriptions must be REWRITTEN — a blanket `Gundabad`→`Goblin` yields "Mount Goblin"); order longest-phrase-first, bare-word catch-all last. Add a post-generation assertion that RAISES (fails the build) on any surviving source phrase in player-facing fields. Use word-boundary `' orc '` (space-delimited), not `orc`, or you mangle armor ids like `sk_md_orc_*`.
- **Source:** memory/feedback_clone_leftover_display_text.md + docs/reviews/rca-new-factions-2026-06-02.md

### Every settlement in settlements.xml needs a matching worldmap scene entity or campaign-map load crashes for everyone
A settlement defined in `settlements.xml` (the external `TAOM_Map` module) MUST have a corresponding entity in the worldmap scene `<game>/Modules/TAOM_Map/SceneObj/Main_map/scene.xscene` whose `name="<settlement id>"` (e.g. `name="town_GT1"`, `name="village_GBC1_3"`). If a settlement exists in data with no matching scene entity, `SandBox.View.Map.Visuals.SettlementVisual.OnStartup()` NREs at campaign-map load and crashes the game for ALL players, not just the settlement's faction.
- **Why missed:** RCA issue #269 (2026-06-02). `OnStartup` does `StrategicEntity = MapScene.GetCampaignEntityWithName(settlement.Id)`; if null it tries a runtime fallback `AddNewEntityToMapScene(...)` then re-gets, but for an orphan that fallback does NOT reliably yield a usable entity, leaving `StrategicEntity` null — then unconditional `StrategicEntity.SetVisibilityExcludeParents(...)`/`GetChildrenRecursive(...)` → NRE. `village_GBC1_4` was added to `settlements.xml` as a placeholder during an earlier deep-review fix, but its scene entity was never placed in the editor (the .xscene had GBC1_1/2/3 but not GBC1_4 — verified by grepping each id: every other new settlement had exactly 1 ref, GBC1_4 had 0). The fix that ADDED the placeholder data created the crash.
- **Prevent:** Diagnosing a `SettlementVisual.OnStartup` NRE — grep the .xscene (readable text) for each settlement id; the one with 0 scene refs is the culprit. When authoring a new settlement, place its scene entity in the worldmap editor in the SAME change as the data, or don't add the data yet — never ship a settlement that's data-only. To stop the crash without the editor, remove the orphan block from `settlements.xml` (UTF-8 BOM + CRLF, back up first) + from `tools/taom_new_factions_layout.json`.
- **Source:** memory/feedback_settlement_needs_worldmap_scene_entity.md + docs/reviews/rca-new-factions-2026-06-02.md

### VillageType ids are code-registered, not XML — verify against the engine (cattle is `cattle_farm`, NOT `cattle_range`)
When authoring `village_type="VillageType.X"` in settlements.xml, X must be a stringId registered by the engine's `DefaultVillageTypes.RegisterAll()` (code, NOT XML — there's no `id="X"` row in SandBoxCore). A plausible-but-unregistered name makes `Village.Deserialize` store a null VillageType; `Village.UpdateTotalProduction()` then dereferences `VillageType.Productions` without a null guard → NRE at the first village production tick.
- **Why missed:** Deep-review HIGH 2026-06-02 (new factions, RCA `docs/reviews/rca-new-factions-2026-06-02.md`) — 7 new villages used `VillageType.cattle_range`, which does not exist; the registered id is `cattle_farm`. Caught by the `/deep-review` API agent decompiling `DefaultVillageTypes` from the installed v1.4.5 DLL, NOT by `validate_moduledata.py` (which neither validates `village_type` ids nor reads the external TAOM_Map settlements).
- **Prevent:** Verify any new `village_type` against the engine registry, or reuse an id already in vanilla/existing settlements (`grep VillageType. settlements.xml`). Valid v1.4.5 ids: swine_farm, cattle_farm, sheep_farm, wheat_farm, vineyard, fisherman, lumberjack, iron_mine, silver_mine, clay_mine, salt_mine, flax_plant, date_farm, olive_trees, silk_plant, trapper. `tools/assign_orc_village_types.py` now has a `VALID_VILLAGE_TYPES` guard that raises at generation time.
- **Source:** memory/feedback_villagetype_stringid_verification.md + docs/reviews/rca-new-factions-2026-06-02.md

### Cross-module XML references require an explicit `<DependedModule>` declaration — the launcher does not infer load-order from XML
When one TAOM-managed module's XML references entities defined in another TAOM-managed module (cultures, factions, troop IDs, party templates, settlement IDs, item IDs), the consumer's `SubModule.xml` MUST declare `<DependedModule Id="<Producer>"/>` + `<DependedModuleMetadata id="<Producer>" order="LoadBeforeThis"/>`. Without it, load-order is accidental — it may work in your launcher profile and break in another player's. Symptoms: deserializer logs "unknown reference X" then silently NULLs the field, or the engine crashes at campaign init when a hideout's `MapFaction` can't resolve.
- **Why missed:** Bandit Management, 2026-05-27 — TAOM Main defined 5 new bandit cultures (`Culture.dunland_raiders` etc.) in `taom_spcultures.xml`; the hideout migration rewrote 99 settlements in `TAOM_Map/ModuleData/settlements.xml` to reference them, but `TAOM_Map/SubModule.xml` declared deps on Native/SandBoxCore/Sandbox/CustomBattle/StoryMode — NOT TAOM. Codex review caught it as HIGH after `/deep-review` missed it. Fixed by adding `<DependedModule Id="TAOM"/>` + the metadata (backup `SubModule.xml.bak`).
- **Prevent:** At authoring time, when writing an XML attribute in module A referencing an entity in module B (both TAOM-controlled), check `B/SubModule.xml` and add A to B's `<DependedModules>`. The launcher sees the explicit declaration, not the logical relationship — applies even when the dependency direction is "obvious." Pre-commit grep gate when editing TAOM_Map ModuleData. External modules (Native, SandBoxCore) have stable well-known load-order and are exempt. Deep-review Agent 5 should trace cross-module XML refs. **Note the LOTRLOME_Armory exception below — that producer is deliberately NOT declared.**
- **Source:** memory/feedback_cross_module_data_dependency_declaration.md

### LOTRLOME_Armory is deliberately NOT declared as a DependedModule — don't flag the gap
`Main/_Module/SubModule.xml` intentionally omits `LOTRLOME_Armory` (and other LOTR asset-only modules) from `<DependedModules>`, even though every TAOM troop XML references `sk_*` items defined there (1,124 refs in `troops_gondor.xml`, 506 in `troops_mordor.xml`, 324 in `troops_dale.xml`). Declaring it breaks the Bannerlord editor (`Win64_Shipping_wEditor`), which enforces module-set constraints the Armory modules don't satisfy. The setup relies on player load-order to ensure Armory loads first at runtime while keeping TAOM editable.
- **Why missed:** Live direction 2026-06-08 during deep-review of the Morannon troop tree — the data-flow agent flagged the missing dependency as a CRITICAL gap; the user confirmed the omission is intentional design, not a bug.
- **Prevent:** When a reviewer/data-flow agent flags `LOTRLOME_Armory` (or any other editor-incompatible asset-only Armory module) missing from `<DependedModules>` as CRITICAL, DROP the finding. Do NOT propose adding the declaration or opening a GitHub issue. This is a documented TAOM-specific exception to the general cross-module-dependency rule above and in `.claude/rules/harness-facts.md`.
- **Source:** memory/feedback_no_depended_module_for_lotrlome_armory.md

### Touching cultural identity means also updating the CC faction-map page (and every kingdom-enumerating config)
Any work touching cultural identity — `Main/Features/CulturalFeats/*`, the `<cultural_feats>` block of `taom_spcultures.xml`/`spcultures.xslt`, party-size/volunteer-respawn/notable-count/terrain-speed/garrison/militia/smithing/raid/morale/loyalty per-culture variants — MUST also update the matching faction in `Main/_Module/ModuleData/factionmap/factions.json`. The CC faction-map sidebar (Perks/Bonuses/Special Units/Strengths/Weaknesses) is the player's primary input when choosing a starting culture; if the in-game effect ships without a matching CC update, the UI lies. Broader: adding a NEW kingdom/culture means updating EVERY kingdom/culture-enumerating config — `taom_spkingdoms.xml`, `taom_spcultures.xml`, `characters/{clans,lords,heroes}.xml`, `diplomacy/diplomacy.json`, `factionmap/factions.json`, `charactercreation/cultures.json`, `execution/alignment.json`, recruitment pools (`VolunteerRecruitmentService` CultureMap), and the C# cultural-feat wiring.
- **Why missed:** Standing instruction 2026-06-01 — the user saw the Gondor CC page still reading the pre-feats "Dunedain Blood: Lords gain experience 10% faster." Separately, `alignment.json` was missed for all 4 new factions (2026-06-02, RCA `docs/reviews/rca-new-factions-2026-06-02.md` Phase 2/3 W1) and caught only by an independent completeness-audit asking "which kingdom-enumerating config has no row for the new factions?" — NOT by `/deep-review` (7 agents) nor Codex (xhigh), both of which reviewed the data that EXISTED rather than enumerating configs that SHOULD have entries. A missing `alignment.json` entry silently falls back to `Neutral`, breaking `TaomExecutionRelationModel` penalties and the `DiplomacyService.IsWarAllowed` same-alignment war-block.
- **Prevent:** When authoring/revising a cultural feat: land it, add to `docs/features/cultural-feats.md`, update the faction's `factions.json` entry (perks/bonuses/weaknesses with correct `positive:` flags), ship the JSON edit in the same/back-to-back commit. For XSLT-wrapped cultures (vlandia/Rohan, empire/Dunland, sturgia/Dale, battania/Khand, aserai/Harad, khuzait/Rhûn): trace whether `spcultures.xslt` OVERRIDES or PASS-THROUGH+APPENDS `<cultural_feats>`, and for each inherited feat decompile vanilla `DefaultCulturalFeats.Initialize` for the CONCRETE text + `EffectBonus` number rather than paraphrasing (Codex caught HIGH 1/2/3 in #260 Phase 2 — `battanian_forest_speed` is "50% less speed penalty + 15% sight range in forests" not "+15% party speed"; `battanian_slower_construction` is -10% not -15%; Sturgia has no forest-speed feat). Add "which kingdom-enumerating config has no row for the new factions?" to every new-faction review.
- **Source:** memory/feedback_faction_map_update_with_cultural_feats.md + docs/reviews/rca-new-factions-2026-06-02.md

### Ported data inherits upstream bugs — pair the fidelity diff with a vanilla-baseline parity audit
A "verbatim" 1-for-1 port of external mod data inherits the upstream's OWN defects: asset-name typos and coverage trims vs vanilla. A fidelity review diffs against the source, so it can NEVER catch what the source itself got wrong — and the 1.4.6 engine crashes on missing action/clip lookups that 1.2 tolerated. Always run BOTH reviews: fidelity diff (catches transcription drift) AND vanilla-baseline parity audit (catches inherited defects).
- **Why missed:** Chariot port 2026-06-12, RCA `docs/reviews/rca-chariot-2026-06-12.md` — ROT's `horse_jump_forwards` was a clip only ROT's packs carried (1.4.6 strict lookup = CTD class), and ROT flattened `base_set` inheritance and dropped 2 fall rows + 5 fall-chain mappings + 1 riderless idle row that vanilla horse carries.
- **Prevent:** When porting external data referencing vanilla assets, ENUMERATE and verify EVERY referenced asset name against vanilla (sampling ships typos). Pick the right baseline: ridden no-BT mounts (chariot) compare against the vanilla HORSE; BT predators compare against the warg (warg-parity law). Applying the spider's 45-row jump-table requirement to a ridden mount produced a false-positive HIGH that even adversarial verification confirmed — verifiers inherit the finder's baseline, so make them challenge the reference class, not just the evidence. A ported asset's OWN doc claiming a property holds for ALL elements is a CLAIM to re-verify against the artifacts (2 of N chariot gait clips were untagged despite `chariot.md` saying all carry `quad_movement`).
- **Source:** memory/feedback_ported_data_upstream_bugs_vanilla_baseline.md + docs/reviews/rca-chariot-2026-06-12.md

---

### A culture block cloned from vanilla ships vanilla CONTENT in every element nobody re-authored
`taom_spcultures.xml` is ~350 lines per culture, and the authoring effort goes to the elements a new
culture obviously needs (troops, party templates, notables, names). Elements that are *valid* with
vanilla values survive the clone untouched: all 14 town-owning cultures shipped
`<basic_mercenary_troops>` pointing at `eastern_mercenary`/`western_mercenary`/`sword_sisters_sister_t3`
(`Culture.neutral_culture`), so Minas Morgul's tavern sold "Hired Pike" for the life of the mod.
Refs that *resolve* are invisible to every validator — `validate_moduledata` checks that
`NPCCharacter.X` exists, not that X belongs to this world.
- **Why missed:** no crash, no log line, no validator, and the sibling element right above it
  (`caravan_guard`) *was* re-authored per culture — which made the tavern look correct on the 30% of
  daily rerolls that use it, hiding the other 70%. It surfaced only as a player screenshot. The
  identical-block signature was the tell: 14 byte-identical copies of a per-culture element is a clone
  artifact, never a decision.
- **Prevent:** when auditing a cloned data block, **diff the cultures against each other, not against
  the schema** — any element whose value is identical across every culture AND equal to vanilla's is
  unfinished until proven deliberate (`git grep -A3 '<element>' | sort | uniq -c`). Second trap in the
  same file: `RecruitmentCampaignBehavior` does not use the id you name — it **randomly walks
  `UpgradeTargets` from it** (each tier 1/1.5×), which is why a T2 root surfaced as its T4 upgrade. Any
  culture element the engine treats as a *root* rather than a *value* needs its whole reachable set
  audited, not just the named entry.
- **Source:** [tavern-mercenaries.md](../../features/tavern-mercenaries.md) (player report, 2026-07-26)

---

### A new custom culture with lords needs child/teen/lord/education equipment templates or new-game NREs
A new TAOM **custom** culture whose clans have lords MUST have entries in the four equipment-template files vanilla's `EquipmentSelectionModel` + childhood-education search by culture, or `InitialChildGeneration` → `HeroCreator.CreateChild` → `GetEquipmentForInitialChildrenGeneration` returns null from an empty culture-match list and NREs in `EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, null)`. Custom cultures inherit NONE of these for free; only XSLT/vanilla cultures do.
- **Why missed:** RCA issue #267 (2026-06-02) — the goblin/mistymountainorcs clone produced troops/npcs/equipment-sets/wanderers but missed all four template categories, so every new game crashed creating a goblin lord's child. `validate_moduledata` does NOT model culture→template-flag coverage and no review agent enumerated it. A "missing Armory item" hypothesis was refuted (a missing item yields an empty slot, not a null `Equipment`).
- **Prevent:** Author (or clone via `tools/insert_new_factions.py`) all four categories in `Main/_Module/ModuleData/equipmentsets/`: `taom_child_equipment_templates.xml` (`IsChildEquipmentTemplate`+`IsLordTemplate`), `taom_lord_template_equipment.xml` (adult `IsLordTemplate` + teen `IsTeenagerEquipmentTemplate`), `taom_education_equipment_templates.xml` (`child_education_equipments_stage_*_<culture>`). Regression-guarded by `ConfigIdValidationTests.ChildGenerationCultures_HaveChildTeenAndLordEquipmentTemplates`. Same "add a faction → update EVERY culture-keyed system" family as [[feedback_faction_map_update_with_cultural_feats]] and [[feedback_clone_leftover_display_text]].
- **Source:** memory/feedback_new_culture_equipment_templates_for_child_gen.md

### Re-themed vanilla cultures: purge the old mapping from EVERY tool, or reports and rebalances lie
TAOM re-themed vanilla `battania` to Khand (Variags, evil), but `rebalance_lords.CULTURE_MAP` still said battania→mirkwood from an earlier era. Every lord report folded 41 Variags into "mirkwood" (71 = 30 elves + 41 Variags), a rebalance would have handed them ELVEN cultural modifiers, and the real Woodland Realm (`Culture.mirkwood`) fell through the map to NO modifiers. Balance decisions were made off the polluted rows for two turns before the generator's `khand: culture_id=battania` entry exposed the conflict.
- **Why missed:** the map lived in a different tool from the one that re-themed the culture; nothing cross-checks `CULTURE_MAP` against `CULTURES[*]['culture_id']` or `taom_spcultures` renames. The polluted "mirkwood" row looked plausible (71 lords, mid-pack stats).
- **Prevent:** when a vanilla culture is re-themed, grep ALL of tools/ for the vanilla id and re-map every hit (fixed in 12b06e47 with the full mapping table in a comment: empire=dunland, sturgia=dale, vlandia=rohan, khuzait=rhun, battania=khand). When two tools disagree on a culture mapping, the generator's `culture_id` + `taom_spcultures.xml` rename are authoritative.
- **Source:** commit 12b06e47; #323 session 2026-07-02.

### Balance passes on shared SkillSets: fork per-culture variants; average targets are pooled-exact only
Lord SkillSets are shared aggressively (the orc trio feeds 6 cultures; `taom_knight_skills` feeds 8). Editing a shared set in place bleeds the change into cultures outside the pass — so a per-culture balance change forks a variant (copy with only the balance skills changed) + `archetype_alias` + a repoint, leaving the base set untouched (verified byte-identical averages for the 6 bystander cultures in #326). Corollary: when several cultures SHARE their sets (3 elf realms, 4 north-orc cultures), a target average lands exactly only POOLED — per-culture results spread ±10 by set-mix composition, and forcing per-culture exactness would need absurd per-canonical residuals (Thranduil at Led ~500).
- **Why missed (near-miss class):** the first nerf draft planned in-place cuts to the orc trio before the usage scan showed mordor + isengard on the same sets; the elf boost would have hit 2 mirkwood lords still parked on `taom_dunland_raider_skills`.
- **Prevent:** every lord balance pass starts with the set-usage scan (culture × set × count, shared-outside flag), forks where shared, bumps culture-exclusive canonicals uniformly (preserves hand-tuned hierarchy order), and ends with the non-target-culture byte-identity check. Codified in `docs/ai-includes/lord-skills-authoring.md` "Per-culture balance variants".
- **Source:** #322/#323/#326 sessions 2026-07-02/03.

### Hand-curating a tiered data set: reserve the top of the RATIONED axis for named exemplars, and let an independent pass check the gradient
When assigning per-fief building levels (221 towns/castles onto lore+role tiers), fortifications is the "rationed" axis: `fort3` must be reserved for capitals + named legendary fortresses only, and culture-flavor deltas must NOT touch it (military flavor lifts siege/barracks, never walls). Two Mordor "major" castles (Barad Wath/Nûrn) got a manual `fort=3` override "because the name means tower" — giving ordinary keeps siege-parity with the four legendary Mordor fortresses and flattening the very tier gradient the standard exists to protect; simultaneously the flagship Rhûn city was under-walled (`fort2`) below the poorer Khand sub-capital (`fort3`).
- **Why missed:** the author sees each fief in isolation — a per-fief "tower → tall walls" call is locally defensible but globally flattens the top of the scale. The inconsistency is only visible when the whole axis is compared across tier-peers and factions at once.
- **Prevent:** pick ONE rationed axis per tiered data set and hard-reserve its max value for named exemplars + capitals; forbid auto-flavor from modifying it. Run an independent adversarial pass (the 7-bloc review workflow judging each value against its tier + cross-faction peers) — it caught all 3 deviations pre-apply. Same "single author can't self-see the seam" class as [[feedback_parallel_builder_shared_subproblems]].
- **Source:** settlement building-levels curation 2026-07-08; docs/features/settlement-building-levels.md; docs/reviews/settlement-buildings-audit-2026-07-08.md.

### Generated troop data: derive weapon identity from EQUIPMENT, never from name keywords — and never let the auditor share the generator's assumption
`rebalance_troops.py` decided weapon specialization from name keywords (`crossbow`/`arbalest` → Bow↔Crossbow swap; `axe`/`spear`/`sword` → melee shifts). Every crossbowman named Sharpshooter/Marksman/Scout/Sniper shipped Bow-top (12 troops, player-visible in the encyclopedia), every two-hander troop named Knight/Berserker/Champion inherited the polearm-biased baselines untouched (59 troops), the `naffatun` keyword swapped two javelin throwers who carry neither bow nor crossbow, and 6 troop names promised weapons the equipment lacked entirely (#344). `analyze_troop_balance.py` imported the same name-derived curve, so ideal == actual by construction — the auditor was structurally blind to the whole class.
- **Why missed:** the 2026-06-25 balance RCA verified curve consistency and refuted findings by name/role reasoning ("an archer shouldn't have Polearm") — the same name-based assumption that produced the bug also hid it. Nothing ever cross-referenced a troop's skills against its equipped items' weapon classes.
- **Prevent:** any generator inferring identity from display names must be driven by the authoritative data instead (`taom_schema.build_item_class_registry`: item id → skill class, reading BOTH vanilla `<Item Type>` and Armory `<CraftedItem crafting_template>` — zero `Type="TwoHandedWeapon"` items exist anywhere, all two-handers are crafted). Writers hard-fail when the authoritative source is unavailable; only read-only tools may degrade, loudly. When fixing generated data, permute in place against a frozen expected id set (abort on divergence / non-permutation) rather than re-running `--apply` over hand-tuned residuals. An auditor must not derive its "ideal" from the generator's own heuristic — give it an independent signal (here: equipment).
- **Source:** #340/#341/#344 session 2026-07-13; docs/features/troop-skill-balance.md "Equipment-driven weapon specialization".
### TAOM's six re-skinned cultures keep their VANILLA StringIds -- key configs on the id, never the display name
`Main/_Module/ModuleData/spcultures.xslt` re-skins the six vanilla cultures by overriding `<name>`, `<text>`, colors and troop refs -- it **never** overrides `id` (grep `attribute name="id"` returns 0 matches). So the real StringIds are: **Rohirrim = `vlandia`, Dunlendings = `empire`, Haradrim = `aserai`, Easterlings/Rhun = `khuzait`, Barding/Dale = `sturgia`, Variag/Khand = `battania`.** There is no culture with id `rohan`, `dunland`, `harad`, `rhun`, `dale` or `khand` -- `taom_spcultures.xml` declares 22 cultures and none of them are these. BannerBearers (2026-07-16) keyed its culture-to-banner map on the LOTR names; all six keys were dead, and six of the mod's highest-volume factions silently flew the generic Gondor standard.
- **Why missed:** the coverage audit regexed `taom_spcultures.xml` only, found 22 ids, *noticed Rohan was absent*, hypothesised "XSLT-transformed" -- and then wrote the config from the LOTR names without confirming what id the XSLT actually emits. A dead key in a `Dictionary<string,string>` is silent at every layer: not a type error, not a parse error, no engine warning; the lookup just misses and returns the fallback. `vanilla-data-comparison.md` documents this rename trap but is `paths:`-scoped to `settlements.xml` / `spcultures.xml` / `*.xslt`, so authoring a JSON config in a new feature folder never loaded it. The knowledge existed; the trigger did not fire.
- **Prevent:** any config that maps culture ids must key on the StringId, and must ship a test asserting **every key** resolves against the real culture set (`taom_spcultures.xml` ids plus the six vanilla re-skinned ids). Validate the KEY side, not just the value side -- validating ids/values is the reflex, and it passes while every key is dead. Generalises to any config keyed on ModuleData entity ids.
- **Source:** docs/reviews/rca-banner-bearers-2026-07-16.md (finding 1, CRITICAL).
### A config's default/fallback value applies to the WHOLE set -- enumerate the set before choosing it
BannerBearers (2026-07-16) shipped `DefaultBannerItemId = "standard_of_duty_t1"` so unmapped cultures would still get a banner. 38 cultures are registered at runtime; the config mapped 28. The unmapped 10 -- `looters`, `sea_raiders`, `forest_bandits`, `desert_bandits`, `mountain_bandits`, `steppe_bandits`, `nord`, `vakken`, `darshi`, `neutral_culture` -- are vanilla leftovers still carrying ~99 live references in TAOM's own ModuleData. The "sensible default" therefore handed the **Gondorian Standard of Duty to every vanilla-culture bandit warband in the game.** Fixed by shipping `""` (fail closed): only explicitly-mapped cultures field standards.
- **Why missed:** the default was chosen for coverage ("everything gets a banner, nothing looks empty") without ever asking *what is in the set that isn't mapped*. The coverage audit asked "are my keys right?" -- a question about the 28 -- and never "how many are there?", a question about the 38. Note the sibling culture-id bug in the same feature was ALSO in this file: fixing six wrong keys says nothing about the cultures that were never keyed at all. All 5 deep-review agents missed it; it surfaced only when the orchestrator counted the registry while writing a Codex prompt.
- **Prevent:** for any config with a default/fallback that applies to unmatched entities, **enumerate the full entity set and read the unmatched remainder out loud** before choosing the default. Prefer fail-closed (empty/none) over fail-open (a plausible-looking value): a forgotten entity with NO value is a cosmetic absence; a forgotten entity with the WRONG value is a live bug wearing a correct-looking mask. Ship a test pinning the default AND asserting the known-leftover entities stay unmapped. TAOM-specific: the culture registry is ~38, NOT the 22 in `taom_spcultures.xml` -- vanilla's `SandBoxCore/ModuleData/spcultures.xml` contributes the rest and many are still referenced by live TAOM data.
- **Source:** docs/reviews/rca-banner-bearers-2026-07-16.md (finding 7, HIGH).
### Swapping a mesh in `skins.xml` means swapping its MATERIALS too -- meshes are `sk_*`, materials are `m_*`
LOTRLOME mesh ids carry an `sk_` prefix (`sk_elf_basemesh_a1_head`) while the matching materials carry `m_` (`m_elf_basemesh_a1_head`), and the two live at different depths: meshes in the `<skin>` element's own attributes (`body_meta_mesh`, `face_meta_mesh`, `legs_mesh`, `hands_mesh`, `underwear_*`), materials in child elements hundreds of lines below (`<face_textures>`, `<mouth_textures>`, `<eyebrow_meshes>`, `<tattoo_materials>`). Re-pointing `face_meta_mesh` to `head_female_a` while `<face_textures>` still names `m_elf_basemesh_a1_head` renders a **garbled face** -- the elf material's UVs don't map onto the vanilla head mesh -- on a correct-looking body, with no error, no log line, and a perfectly well-formed file.
- **Why missed:** 2026-07-23 female-elf basemesh swap. Verification grepped the edited blocks for `sk_elf_` (the MESH prefix), got zero hits and reported "clean"; the surviving `m_elf_` material refs were never in the search space, and the broken face reached the user's screen. The search string was derived from the strings that had been EDITED rather than from the strings that could still be WRONG -- so it could only ever confirm the edit, never the result. Well-formedness checks were silent because every defect was semantically wrong and syntactically valid.
- **Prevent:** after any `skins.xml` mesh swap, search the block for the RACE TOKEN rather than a prefixed id -- `grep -oE '(sk_|m_)[a-z_]*(elf|dwarf|uruk|orc|goblin)[a-z0-9_]*'` over the block's line range must return empty. Better: parse both files and diff the whole `<skin>` subtree against the equivalent skin in `Native/ModuleData/skins.xml`; that single check surfaced the material mismatch, a dwarf mouth material and missing eyebrows in one pass, all of which the grep missed. Note no automated gate covers `skins.xml` -- `tools/Audit-MeshRefs.ps1` reads only `mesh=` attributes and never sees `body_meta_mesh=`/`face_meta_mesh=`.
- **Source:** docs/reviews/rca-elf-female-skins-2026-07-23.md

### Prefab folders have a hard entity budget — 131,072 `<game_entity>` across all loaded `Prefabs\` XML
The editor (and game) enqueues every `<game_entity>` from every loaded module's `Prefabs\*.xml` into a native parallel-load queue hard-capped at 131,072 items (chunk 4096 × 32 — EXACT, disassembled from the wEditor `TaleWorlds.Native.dll` queue push at RVA `0x7708F0`). Crossing it fires the `rglConcurrentQueue.h:882`/`:969` assert dialog at startup; **Ignore corrupts the queue** (loader hangs forever), Abort crashes at a *secondary* site (`0xf5e7b5`, null-global deref) that is what WER records — so the Event-Log offset does NOT name the real problem. TAOM_Map hit 132,378 entities on 2026-07-24 after ~40.7K entities of imported prefab packs landed in one batch.
- **Why missed:** the folder grew organically (49→80 MB over months) with no gate; the assert fires before engine logging initializes when it triggers during the early module scan (0-byte watchdog log, no rgl_log), so nothing searchable ever recorded it; and the assert text isn't documented anywhere public. The near-miss state (just under cap) was indistinguishable from healthy.
- **Prevent:** `python tools/check_prefab_budget.py` (warn >120K, error ≥cap) before adding prefab packs; scene-usage classification (`references.txt` `prefab` records + `scene.xscene prefab=` union + transitive closure) parks anything no scene uses in `Prefabs_Unused\` — see the re-enable workflow + inventory in the investigation doc. On any engine assert dialog: it's a paused pre-crash state — dump the process and copy the rgl logs BEFORE clicking anything, and never Ignore a queue/invariant assert.
- **Source:** docs/investigations/editor-rglconcurrentqueue-assert-2026-07.md

### When TAOM data feeds an engine consumer that validates nothing, diff the whole vanilla corpus for the implicit invariant — then pin it with a build-time test
`<banner_bearer_replacement_weapons>` is consumed by `SandboxBattleBannerBearersModel.GetBannerBearerReplacementWeapon` with a tier-match and **no weapon-class filter**; the engine equips the result alongside a `HeldInOffHand + DropOnWeaponChange` banner. Every vanilla culture ships only 1H swords there — an invariant that exists nowhere in schemas, docs, or code, only in the uniformity of vanilla's data. TAOM gave Mirkwood three `TwoHandedPolearm` CraftedItems (and Isengard two pikes); the first reinforcement banner-bearer spawn ~6 minutes into a siege hit the engine's unguarded native slot-4 read as a `0xC0000005` CTD (#360).
- **Why missed:** the culture data validated clean (`validate_moduledata` checks refs exist, not weapon-class semantics), the feature's own review checked that replacement weapons were *declared*, and the invariant is invisible per-file — it only appears when you ask "what do ALL vanilla cultures have in common here that TAOM broke?" No agent prompt asked that question.
- **Prevent:** when authoring data an engine model consumes without validation, enumerate the vanilla corpus for the same element and treat any uniform property (all 1H, all non-crafted, all a given Type) as a load-bearing invariant until the decompiled consumer proves otherwise; pin it with a build-time test classifying against the **installed** modules (`BannerBearerReplacementWeaponDataTests` is the template — game-dir resolution via `GameAssemblies`, `Assert.Inconclusive` off-machine). Sibling rule: "broadening an engine call re-opens every precondition its vanilla callers relied on" (adapters-taleworlds-api.md) — this is its data-side form, and the precondition scope is mission-lifetime, not just deployment-time.
- **Source:** docs/reviews/rca-banner-bearers-reinforcement-av-2026-07-25.md
### When one data layer overwrites another at runtime but tests only exercise the overwritten one, passing tests describe a configuration the game never runs
Gondor recruitment pools exist twice: `ModuleData/recruitment_pools/gondor.json` and the hand-written `InitializeGondorSettlements` C# fallback. The JSON overwrites `SettlementMap` at runtime, so the C# layer is live only in degraded mode — **and in the unit tests**, because the auto-loader resolves a game-relative path that does not exist in the test bin. The two silently diverged: the C# side stranded the whole 7-troop Ithil Guard line, pooled three ids the JSON never offered, and assigned `castle_EW10` the wrong region's troops. The drift was not merely undetected; it was encoded into passing `[DataRow]` roll expectations. A 2026-06-24 instance of the identical inversion (the Ithilien Ranger live at 0% while fallback tests stayed green) was fixed pointwise for that one troop instead of structurally.
- **Why missed:** the test-bin inversion is counter-intuitive — the harness exercises the *fallback* precisely because the production path needs a game install. Every per-file review sees two internally-consistent files; only a cross-layer comparison sees that they disagree. Completeness review cannot catch it either, since "tests exist and are green" is true and is the problem.
- **Prevent:** whenever a runtime loader OVERWRITES a compiled-in data layer, ship a lockstep test asserting the two encode the same distribution, comparing **normalised shares** rather than raw values (the layers legitimately use different scales). Treat a pointwise fix to one drifted entry as a signal to add the structural gate, not as the fix. Sibling rule: "a doc-vs-config consistency check cannot catch a defect present in both" (testing-qa.md) — same shape, different pair.
- **Source:** docs/reviews/rca-gondor-recruitment-2026-07-27.md (F3).

---

### Rarity is a tier signal, not an underuse signal

An equipment-variety sweep over the Erebor/Iron Hills rosters optimised for "spread the least-used items" and reached straight for end-tier exclusives — they are rare precisely BECAUSE only one level-46 troop wears them. 25 items dropped ten or more wearer levels; the royal warden's cuirass landed on a level-11 recruit (−35), and the strongest 1H axe in the culture spread from level-46-only down to level 21.
- **Why missed:** the objective function was inverted for exactly the items it most wanted to place, and every automated gate passed on the broken file — `validate_moduledata.py` PASS, `validate_all_troop_refs.py` PASS, build clean, 4,529 tests green. Referential integrity and tier sanity are orthogonal, and TAOM validates only the first.
- **Prevent:** when redistributing game content by usage frequency, derive a tier floor per item from the lowest-level entity already using it and never place below it. In an armoury with no tier field, the existing assignments ARE the tier data. Assert afterwards that no item's minimum wearer level decreased.
- **Source:** docs/reviews/rca-erebor-equipment-sweep-2026-07-30.md (F1–F4).

### A stat you cannot find on the item may live one indirection away

Dwarf melee weapons are `<CraftedItem>` elements with no `<Weapon>` child; reach and damage come from the referenced `CraftingPiece` `BladeData`. A stat comparison read `<Weapon>`, found nothing for the whole class, and silently compared nothing — so a 20-unit stub blade and a 44-unit greataxe scored identical, and a level-36 specialist was handed the stub while a level-21 crossbowman's sidearm got the greataxe.
- **Why missed:** the gap was noticed mid-implementation and guessed past — "stats derive from crafting pieces, likely comparable" — rather than looked up. `.claude/rules/troops.md` already mandates grepping weapon stats before tier-ordered picks; the rule was loaded, quoted, applied to ranged weapons, then not applied to melee because the stats were one indirection away instead of on the item.
- **Prevent:** treat an empty stat lookup across a whole item class as an unfinished lookup, never as "no constraint." Extend the grep-the-stats habit to piece tables for crafted items.
- **Source:** docs/reviews/rca-erebor-equipment-sweep-2026-07-30.md (F3).

### A ref that looks like a typo may be the asset's real name — check the TOC before correcting it

`wm_isengard_shield_a04` names `body_name="bo_capwm_isengard_shield_a02_clean"`, missing the underscore that all 224 sibling shields have. #352 makes a malformed `body_name` a hang, so the reflex is to correct it. The packaged PhysicsShape TOC says otherwise: `bo_capwm_isengard_shield_a02_clean` exists, and the corrected `bo_cap_wm_isengard_shield_a02_clean` exists in no `.tpac`. "Fixing" the XML would have created the exact infinite mission-load hang #352 documents. The same audit found `gond_shld4` using its full body as its own capsule — also unfixable, because no `bo_cap_wm_gondor_shield_a` was ever built.
- **Why missed:** #352's lesson is stated one-directionally — "the assets shipped fine, the refs were a suffix off" — which trains the reflex "malformed ref ⇒ fix the ref." A ref and its asset are a pair, and either half can be the odd one. `validate_mesh_refs.py` reported PASS on the misspelled name, which reads like a tool gap and is actually the answer.
- **Prevent:** before rewriting any `body_name` / `mesh` that looks wrong, query the TOC for BOTH spellings (`build_present_set(...).physicsshapes`). A PASS from `validate_mesh_refs.py` on a suspicious name is positive evidence the name is correct. Only names it flags `MISSING_BODY` are safe to rewrite — and when neither spelling resolves, the fix is to build the asset, not to guess a sibling's.
- **Source:** docs/reference/armory-shield-audit.md (shield `item_usage` audit, 2026-08-03).

### A ModuleData file the CLIENT engine tolerates can still be FATAL to the DEDICATED-SERVER engine

`LOTRLOME_Armory/ModuleData/action_sets.xml` carried 168 `<action>` elements parented by
`<action_sets>` instead of by an `<action_set>`. Twelve `as_<race>_female_villager_in_aserai_tavern`
sets — dwarf, uruk, uruk_hai, berserker, orc, nazghul, hill_troll, pale_uruk, cave_troll, dg_uruk,
goblin, saruman — were authored SELF-CLOSING, which orphaned the 14 female-conversation overrides
that belong nested inside each (vanilla's own `as_human_female_villager_in_aserai_tavern` nests
exactly those 14, in that order; all twelve TAOM groups matched it). Build **1.4.7.117484**, the
game client, tolerates the file in silence. Build **117131**, which TaleWorlds' dedicated-server
engine ships, throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at schema path
`/action_sets/action` and dies on boot — which is why server operators had to fall back to the
single-player module order (Alliance.Wargs before LOTRLOME_Armory) or crash.
- **Why missed:** malformed structure that produces no symptom in play is not validated by playing,
  and every hour this file had ever received was on the client build. `audit_action_set_parity.py`
  asked whether each humanoid set carries the full `as_human_warrior` surface — a COVERAGE question —
  and had no opinion about parentage, so a file 168 elements structurally wrong passed it clean.
  Note the generator did **not** produce these: the broken sets sit outside
  `tools/generate_race_civilian_action_sets.py`'s `TAOM-CIVILIAN-COVERAGE:START/END` marker block and
  were hand-authored. Grepping for a generator is the obvious first move and it accuses the wrong
  component — sibling entry "A review finding's stated CAUSE can be wrong even when the finding is
  right" in [build-tooling-workflow.md](build-tooling-workflow.md).
- **Prevent:** a structural validator must assert the schema SHAPE, not merely that the file parses,
  and must exit non-zero so a gate can consume it — `audit_action_set_parity.py` now reports
  root-level `<action>` elements and exits 1 on them as well as on humanoid gaps. Repair with a fixer
  that is idempotent and updates the LIVE file and the tracked snapshot together
  (`tools/oneoff/fix_orphaned_tavern_conversation_actions.py` →
  `docs/reference/lotrlome-armory-snapshot/action_sets.xml`); 34,247 action elements and 1,226
  action_sets survived unchanged, only parentage moved. Generalise: when a data file feeds more than
  one engine BUILD, "it works in game" is evidence about one of them. A dedicated-server boot
  verifying this fix is still owed.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit c9455ec8

### A per-entity validity check cannot see a set-relative defect — a settlement entrance can be VALID and unreachable

`PathFaceRecord.IsValid()` returns **true** for all three settlement destinations reported wedging AI
parties in 2026-08-03 field testing: town_MM2's gate (676.8127, 994.5818, face 17541),
hideout_desert_7 (1072.412, 374.593, face 3928), castle_village_MM1_2 (725.024, 1032.055, face
17014) — all three byte-exact against the live `TAOM_Map/ModuleData/settlements.xml`. Nothing is
off-mesh. Each face simply belongs to a navmesh ISLAND the rest of the map has no path to, so every
AI tick targeting one fails its path query and the engine's only report is a repeating "Path finding
target is not valid" assert that names no settlement. This is the quiet cousin of the orphan-scene-
entity entry above: that one is an ABSENT entity producing a hard NRE for everyone, this one is a
PRESENT, well-formed entity producing a silent AI wedge.
- **Why missed:** every check available operates on one face at a time, and each of the three faces
  passes. Reachability is not a property of a face — it is a property of a face RELATIVE to the rest
  of the mesh — so no per-entity validator can express it, and the failure signal (an engine assert
  with no id in it) never reaches a log anyone reads.
- **Prevent:** compare against the SET. `PathFaceRecord.FaceIslandIndex` is the engine's own
  connected-component id — two faces with different indices have no path between them at any cost —
  so the main landmass is the island index the most settlements agree on, and any settlement
  disagreeing is unreachable. `taom.audit_settlement_entrances`
  (`Main/Features/DevConsole/Cheats/SettlementEntranceCheats.cs`) walks every settlement's entrance
  (`GatePosition` for towns/castles, else `Position`), flags the disagreements, and emits an
  engine-computed replacement from `IMapScene.GetAccessiblePointNearPosition` at widening radii
  1/2/4/8/16/32. **Status: the auditor ships, the corrected coordinates do not exist yet** — one
  in-game campaign run is needed to produce them, and they then go into the LIVE
  `TAOM_Map/ModuleData/settlements.xml`, never the repo's stale shadow at
  `Main/_Module/ModuleData/settlements.xml`.
- **Source:** Multiplayer field report 2026-08-03 (TAOM v2.0.16 co-op testing); commit 31405eb1

### A culture that owns no settlement is a latent CTD, not a cosmetic gap

`HeroSpawnCampaignBehavior.SpawnLordParty` (v1.4.7, lines 252-260) ends its spawn-settlement fallback
chain on `Settlement.All.First(x => x.Culture == hero.Culture)` — an unguarded `First`, not
`FirstOrDefault`. Reaching it needs two independent faults at once: the hero's map faction has no
`InitialHomeSettlement` (`GetBestSettlementToSpawnAround` weights own-faction settlements 10,000×
higher, so the mismatch branch above only fires for a landless faction), **and** the hero's culture
owns zero settlements. TAOM supplies the second fault at scale: `TAOM_Map/ModuleData/settlements.xslt`
deletes all 494 vanilla settlements and TAOM_Map's 988 replacements cover 27 of the 38 defined
cultures. Of the 11 landless cultures, five can sit on an `Occupation.Lord` hero — `battania` (TAOM's
Variag), `darshi`, `nord`, `vakken`, `neutral_culture`; the six bandit cultures are landless but
unreachable, because `GetBestAvailableCommander` filters on `Occupation.Lord` and bandit heroes are
`Occupation.Bandit`.
- **Why missed:** nothing fails at load, at validation, or for weeks of play — crash bundle
  `099f650c` is Summer 2 / 1084, past 90 in-game days. When it does fire it arrives from
  `CampaignEvents.DailyTickClan` with **no TAOM frame in the stack** (the bundle's own Harmony
  correlation shows no patch, TAOM's or any other mod's, on frames 0-11), so it reads as an engine or
  third-party bug rather than a TAOM data defect. The landless culture is not the whole cause either
  — it is one of two halves, and the other half (a faction with no `InitialHomeSettlement`) is
  contributed by a runtime clan-creating mod, which is why the same data sat harmless in every
  campaign that did not run one.
- **Prevent:** `tools/taom_schema.py` `_landless_cultures()` (pass 4b) now ERRORs `LANDLESS_CULTURE`
  for any culture used by an `occupation="Lord"` NPCCharacter, a `<Faction>` or a `<Kingdom>` in
  TAOM's ModuleData that owns no settlement. Treat "which settlements carry `culture="Culture.X"`?"
  as an authoring gate for every culture with lords, not a cosmetic question. The engine-side
  backstop is `Patch65_LandlessCultureSpawnGuard`, which repairs the *other* half of the
  precondition — data validation cannot cover a faction that a third-party mod creates at runtime.
- **Source:** #374 + [lord-spawn-guard.md](../../features/lord-spawn-guard.md) (crash bundle
  `099f650c`, 2026-08-04)

### A culture can be fully authored and still own nothing — "it validates" is not "it is usable"

TAOM's `battania` is the Variag/Khand culture: `{=TAOM_battania_culture}Variag`, a 7,297-char template
in `spcultures.xslt`, 26 `notable_templates`, 68 `NPCCharacter.*_khand` bindings that ALL resolve
against the full `NPCCharacter` registry, a kingdom, `clan_battania_1`..`_8`, `wolfskins`, plus 41 surviving
vanilla `Lord` characters and 18 TAOM-authored ones. It owned **zero settlements**: all 27 K-series
settlements carried `Culture.khuzait` (Easterlings) while Variag clans held ten of them, so Khand
produced Easterling notables, volunteer pools, guards and marketplace stock. Retagging 26 of them to
`Culture.battania` then activated bindings that had been dormant precisely *because* nothing carried
the culture — `basic_troop` was still vanilla `battanian_volunteer`, an id TAOM redefines nowhere, so
Sturlurtsa Khand would have garrisoned Calradian Battanian militia (fixed by repointing the five
militia/basic bindings + `default_party_template` at the Rhun roster those settlements already
produced).
- **Why missed:** every per-file check passes. `validate_moduledata` proves the 68 refs resolve; no
  check asks whether the culture is ever instantiated. `CultureConversion` could not repair it either
  — `CultureConversionService.RunDailyChecks` drains only records already in the store, and the store
  is seeded exclusively by `OnSettlementConquered`, so a settlement whose culture never matched its
  owner *from day 1* is never enqueued (the crash log confirms it: 90+ days, zero conversion lines).
  A `[CultureMarketplace] town_K1 (battania)` debug line read as proof conversion had already run;
  `TownRosterAdapter.GetCurrentCultureId` returns `settlement?.OwnerClan?.Culture?.StringId` — the
  OWNER's culture, not the settlement's. The diagnostic's label names a different field than its name
  implies.
- **Prevent:** the acceptance question for an authored culture is which settlements carry it, not
  whether its refs resolve. When a culture goes from 0 settlements to N, re-audit every element the
  clone left at vanilla values BEFORE shipping the retag — this is the deferred-detonation form of
  "A culture block cloned from vanilla ships vanilla CONTENT in every element nobody re-authored"
  above, where the vanilla leftovers stayed invisible for years because no settlement ever asked for
  them. Retag with an explicit id allowlist, never a prefix sweep: `castle_K4` is a genuine Easterling
  holding inside the K-series and a `town_K*`/`castle_K*` sweep would have taken it
  (`tools/oneoff/retag_khand_to_variag.py` — dry-run default, asserts the current culture before
  writing, idempotent).
- **Source:** #374 + [lord-spawn-guard.md](../../features/lord-spawn-guard.md)

---

---

### Derive a settlement id set from the relationship graph, never from an id-prefix convention
Migrating or retagging a settlement cluster (culture change, rebellion, map expansion) must enumerate its members from `<Village bound="Settlement.X">` / `owner=` — the actual graph — not from a naming convention like `village_K*`. TAOM's map uses TWO village naming schemes: `village_KN_M` hangs off town `town_KN`, and `castle_village_KN_M` hangs off castle `castle_KN`. A prefix-derived set silently drops the second family.
- **Why missed:** the Khand retag (#374, 2026-08-04) enumerated 26 ids from the `village_K*` convention and missed 18 `castle_village_K*`, splitting every Khand fief group across two cultures — a Variag castle whose villages spawn Easterling headmen (`NotablesCampaignBehavior` -> `settlement.Culture.NotableTemplates`) and draw the Easterling villager party template. Nothing enforced the village-culture ↔ bound-parent-culture relationship, so nothing complained. This is the shape the "you have verified a coincidence with good hygiene, not an invariant" entry above warns about: the property held 607/607 across the whole map purely by authoring discipline.
- **Prevent:** parse the settlement graph (`./Components/Village[@bound]` — it is nested under `<Components>`, NOT a direct child of `<Settlement>`, which is its own trap) and derive the id set from it. Then run a pre/post audit asserting village culture == bound-parent culture across ALL 607 bound villages; it takes ten lines and it is what proved both the break and the fix. Land the invariant as a `validate_moduledata.py` check in the same PR so the next retag cannot reintroduce it.
- **Source:** `docs/reviews/rca-landless-culture-spawn-2026-08-04.md` (deep-review M1, 2026-08-04)

### The troop roster is already a controlled experiment — read it as one before theorising

A visual defect reported as "all the X troops look wrong" arrives described in whatever vocabulary the
reporter had. That vocabulary silently becomes the investigation's scope. Before forming hypotheses,
tabulate the affected and unaffected troops against **every** factor that varies independently —
race, `face_key_template`, armour family, per-slot items — and look for a row that breaks the reported
correlation.

- **Why missed:** #389 was reported as "Uruk-Hai and berserkers", which maps cleanly onto two
  `skins.xml` race entries, so a 21-agent sweep with adversarial verification spent a day scoped to
  the race. The roster already contained the disproof: `urukhai_recruit` is race `uruk_hai`, uses
  `BodyProperty.fighter_uruk_hai`, wears an `sk_uruk_hai_*` body mesh — and renders correctly. It is
  the only Uruk-Hai troop with **no Head item**. One observation exonerated the race, the
  body-property template, the base-body meshes and the whole body-armour family at once.
- **Prevent:** build the factor matrix FIRST and name the troop that isolates each factor
  (in Isengard: `orc_warg_scout` = uruk race + orc body-property + non-uruk armour;
  `isengard_militia_*` = uruk race + human body-property; `isengard_orc_berserker` = orc race +
  berserker body-property; `urukhai_recruit` = uruk race, no helmet). Ask the reporter to open those
  specific troops rather than accepting the reported set as the true set.
- **Source:** #389 / `docs/reviews/rca-isengard-black-tableau-2026-08-06.md`

### `shader_compile_report.log` is a free mesh → material map (and one mesh has many rows)

`LOTRLOME_Armory/Shaders/D3D11/shader_compile_report.log` is plain text listing every compiled asset as
`mesh  material  shader  variantCount`. It answers "what material does this mesh actually use" without
a `.tpac` parser, and a **variant count far above the norm** (888 vs the usual 120) marks a *skin*
material, because skin materials compile the morph/skin permutations.

- **Why missed:** every prior asset sweep in #389 checked base-body meshes and never the equipment,
  and reached for binary tpac scans when a grep of this file would have answered it.
- **Prevent:** grep this file before decompiling or byte-scanning packages. **Never use `grep -m1`** —
  one mesh legitimately carries several material rows (a bracer includes hand geometry), and the first
  row alone is actively misleading. It is what exposed that every `sk_uruk_hai_helmet_*` bundles
  `m_uruk_hai_gloves_a1` + `m_uruk_hai_hands_a1` (888) alongside its helmet material, where the
  working `sk_gn_orc_mrd_helmet_light_a` control carries exactly one.
- **Source:** #389 / `docs/reviews/rca-isengard-black-tableau-2026-08-06.md`

### A bracketing constraint must be shown non-empty before you design to it

"Above the uruk blade, below hero kit" is worthless as a target if hero kit is already below the uruk
blade, which it is: `sm_uruk_sword_blade_a3` cuts at 3.74 and `wm_witch_king_sword_blade` at 3.50. A
whole weapon family shipped at 3.8 to 4.0 cut, beating every hero blade in the game, because the
bracket was adopted from two looked-up examples rather than from the sorted population.

- **Why missed:** anchoring on the two or three comparators already in hand, and treating "I checked
  some real examples" as "I checked the population". The blade that invalidated the premise was in a
  file already read that session.
- **Prevent:** before designing to a bracket, sort the actual population on the axis in question and
  confirm the bracket is non-empty. It is one script over data already on disk.
- **Source:** `docs/reviews/rca-black-numenorean-2026-08-17.md` finding 1.

### Item-tier correct and wearer-level correct are two different checks

Mapping mesh tier names one-to-one onto curve tiers is right for the ITEMS and says nothing about the
WEARER. A light-tier hood is perfectly statted and still wrong on a level-26 troop: the Black
Numenorean Initiate shipped at 50 total personal armour against a level-26 cohort median of 157, the
lowest of 157 troops at that level.

- **Why missed:** curve conformance was verified exactly (78/78 items matched `calculate_stats`) and
  read as "the armour is balanced". `derive_armor_tiers.py` cannot adjudicate it either, because
  `derive()` applies the id keyword before consulting the roster anchor, so a `_light_` id always
  reports `delta: 0`.
- **Prevent:** for every new troop, compare total personal armour against the other troops at the
  same `level=`. Compare across cultures too, not only within one: the same pass shipped shields
  above Gondor's ceiling while looking correct against Mordor's own ladder.
- **Source:** `docs/reviews/rca-black-numenorean-2026-08-17.md` findings 2 and 7.

### Never select a game item by what its name implies

`charger` sounds like a knight's warhorse and is slower and weaker-charging than `t2_empire_horse`
(speed 48 / charge 22 against 50 / 26). A tier-8 "promotion" therefore downgraded the mount.

- **Why missed:** mounts were picked by name semantics; the stat block was never opened.
- **Prevent:** read the stat block. The same rule retires invented values: six `merchant_cost`
  entries were authored without checking the field is only consumed for troops listed in
  `elite_emissary_config.xml` `<CultureOffers>`.
- **Source:** `docs/reviews/rca-black-numenorean-2026-08-17.md` findings 6 and 12.

### A new line whose display name contains a tier keyword is mis-tiered by the shared balance tooling

`rebalance_armor.detect_tier` keyed on an `elite_keywords` list containing the literal string
`'black numenorean'`. Every item in the new set is named `[Mordor] Black Numenorean <something>`, so a
**light** hood classified as elite: 45 of 78 mis-tiered, and `rebalance_armor.py --apply --cultures
mordor` would have flattened the whole set onto the elite row.

- **Why missed:** the question "what will the shared balance tooling make of this id prefix and this
  display-name convention?" was never asked.
- **Prevent:** when adding a culture or line, grep the keyword lists in `rebalance_armor.py`
  (`elite_keywords`, the lord/hero lists) for any word in the new display-name convention before
  authoring. A line name sitting in a tier-keyword list is the defect.
- **Source:** `docs/reviews/rca-black-numenorean-2026-08-17.md` finding 3.

### An authoring generator can emit three of four weapon descriptions and look complete
`sm_md_num_lance_a` shipped on 2026-08-18 (`2fcbef10`) registered under `TwoHandedPolearm`, `TwoHandedPolearm_Bracing` and `TwoHandedPolearm_Couchable`, and **not** under `OneHandedPolearm`. That is the one description whose usage set permits a shield, and `OneHandedPolearm` is listed first in Native's `TwoHandedPolearm` template, so it is also the one that decides the PRIMARY usage. The lance therefore resolved to `polearm_block_long_shield_thrust`, flagged `requires_no_shield`, on 8 cavalry rosters that all carry a shield. Nothing errors and nothing logs: the troop holds the lance through the pre-battle phase, because spawn wield is plain slot order, then draws its sidearm the instant the fight starts and never touches the lance again. This is the identical defect the four Dale spears had (#449), authored by hand; the point of this entry is that it arrived the second time out of a **generator**, three weeks later, in a culture nobody associated with Dale.
- **Why missed:** three of four is the shape that defeats review. A reviewer greps the new item's piece ids, finds them present in `weapon_descriptions.xslt`, and stops, because the registration plainly exists. Only counting which descriptions are missing surfaces it, and there is no natural prompt to do that. The bracing and couch registrations actively mislead here: they are the ones a lance obviously needs, so their presence reads as "the polearm wiring was done". The couch usage cannot rescue it either, since `polearm_couch` is `passive_usage` and only engages once the weapon is already wielded. And `ItemUsageSetFlags.RequiresNoShield` has exactly one managed consumer, a UI tooltip in `CampaignUIHelper.cs`; the AI behaviour that refuses the weapon is native, so no decompile search for the flag leads to the code that acts on it.
- **Prevent:** `python tools/audit_polearm_shield_parity.py` resolves each crafted weapon's primary usage the way `Crafting.cs:566-608` does and fails on any roster pairing a shield with a `requires_no_shield` polearm. Run it after **any** weapon-authoring generator, not only after hand edits, and treat the generator itself as the thing under test. When a new crafted weapon lands, enumerate the descriptions it is *absent* from rather than confirming the ones it is present in. The registration itself belongs in `tools/register_one_handed_polearms.py`: add the item id to `ONE_HANDED_ITEMS`, never hand-edit the Armory XSLT, since that file is not in this repo and a module refresh reverts it silently.
- **Sibling entries:** same family as "The lance must exclude `swing`" in [black-numenorean.md](../../features/black-numenorean.md) and `rca-crafting-usage-features-2026-07-26.md` (20 mace heads). All three are one root cause: a crafted weapon's behaviour is assembled from description membership and usage-feature tokens, never declared on the item, so an incomplete assembly is silent by construction.
- **Source:** 2026-08-20, merging PR #447. The gate the PR added found this on its first run against trunk.

### Reusing a vanilla lord id silently inherits its sex, and the beard block is what renders
TAOM reuses vanilla NPCCharacter ids for entirely different characters. `lord_WE8_c` is vanilla's
Icratia, a woman, reused for Pelendur son of Golasgil. `lord_1_46_1` is vanilla's Seorgys, a man,
reused for Thorwen, Malrior's wife. `lord_4_6` is Countess Calatild, reused for Grimbold of
Grimslade. In all three the rename landed and `is_female` did not, so the roster shipped a man
rendered as a woman and two women rendered as men. Two Lothlorien elf-ladies (`lord_L2_5`
Nimlothiel, `lord_L3_3` Silivren) had the same shape from a different route: `is_female="true"` on
`taom_elf_lady_skills`, and a `<beard_tags>` block left in the face.

- **Why missed:** `is_female` is one attribute in a 200-line block, and every OTHER layer had already
  been updated, which is what makes this class invisible. The registry, all 12 language files, the
  encyclopedia bio and the `father`/`mother` wiring in `heroes.xslt` all described Pelendur as
  Golasgil's son while the character data said woman. Reviewing any one of those layers confirms the
  intent and never reaches the defect. The second trap is that `is_female` alone does not undress a
  character: `<beard_tags>` is what actually renders facial hair, so flipping the attribute without
  deleting the block leaves a bearded woman, and deleting the block without the attribute leaves a
  clean-shaven man the engine still treats as female.
- **Prevent:** `TAOM.Tests/Core/LordNameAndSexConsistencyTests.cs` asserts no `is_female="true"` lord
  carries `<beard_tags>` (it accepts the six `is_female="True"` entries too, since the engine's bool
  parse tolerates either casing). Nothing else can see this: `validate_moduledata.py` has no rule
  touching sex, and the body-properties key is authored inline rather than as a `BodyProperty.*`
  reference, so `BROKEN_BODY_PROPERTY_REF` never fires on a female key worn by a man. When you reuse
  a vanilla id, treat `is_female`, `<beard_tags>` and the `<BodyProperties key=>` as one unit and
  change all three or none. The prose is the spec: the bio in `heroes.xslt` and
  `taom_xslt_strings.xml` says son / daughter / wife, and it is the cheapest thing to check against.
- **Source:** 2026-08-28, `a00086da`; the same class shipped three weeks earlier as `3c7f4e25`
  (Grimbold) with no lesson recorded, which is why it recurred.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/faction-map.md](../../features/faction-map.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/modding/clans.md](../../modding/clans.md)
- [docs/modding/editing-safely.md](../../modding/editing-safely.md)
- [docs/modding/troubleshooting.md](../../modding/troubleshooting.md)
- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)
- [docs/reviews/lessons/xslt-moduledata.md](./xslt-moduledata.md)
- [docs/reviews/rca-townsfolk-sex-2026-09-06.md](../rca-townsfolk-sex-2026-09-06.md)

<!-- backlinks-end -->
### A mesh validator that resolves against cooked packs cannot see art deleted after the last cook

`validate_mesh_refs.py` builds its present-set from `AssetPackages/pack0-9.tpac`. Those are
rebuilt only on an explicit re-cook, so art deleted from `Assets/` or `AssetSources/` keeps
shipping from a stale pack. On 2026-08-28 four asset-repo commits deleted 755 files and the
validator still returned `PASS: no mesh-reference issues found`, sitting on top of 179
already-deleted meshes that 149 item references still named. The breakage was one re-cook away
and invisible until then.
- **Why missed:** the tool was trusted as "the mesh check" without asking which tree it reads. A
  `PASS` is a strong signal and nothing distinguished it from a `PASS` on genuinely clean data.
- **Prevent:** treat the two trees as separate sources of truth and diff them. `AssetPackages`
  minus `Assets` is art that is gone but still shipping (breaks at the next cook); `Assets` minus
  `AssetPackages` is art imported but not cooked (renders naked NOW). Both are real defects and
  they are opposite, so a tool reporting one must not be read as covering the other. The two
  questions also give different counts on purpose: "imported but not cooked" is wider than
  "referenced by an item and not cooked", because the latter only walks item XML.
- **Source:** docs/features/armoury-mesh-cleanup.md; tools/audit_deleted_mesh_impact.py.

### Regional armour sets have uneven slot coverage, so "their own region's gear" is not always possible

Re-dressing the five named Gondor lords from their own regions looked like a lookup and was not.
Measured 2026-08-28: **Dol Amroth is the only Gondor region shipping all five slots.** Lamedon and
Anfalas ship no gloves, greaves or cape at all; Lossarnach and Pinnath Gelin ship no greaves.
Anfalas ships only infantry gear, so its own top tier is heavy rather than lord.
- **Why missed:** the culture reads as complete because body and head armour are well covered in
  every region, and those are the slots checked first. Gloves, greaves and capes are authored far
  more sparsely, and nothing surfaces the gap until a specific slot has to be filled.
- **Prevent:** before promising region-faithful equipment, enumerate surviving items per region
  AND per slot, with armour values, and decide the fallback explicitly (here the generic lord-tier
  Serelond bracer and greaves). Also compare tiers before swapping: the bespoke lord pieces were
  85 body / 50 head / 35 gloves and nothing any region ships exceeds 70 / 41 / 27, so the swap was
  a real nerf that had to be stated rather than discovered later.
- **Source:** docs/features/armoury-mesh-cleanup.md; docs/reference/armory-guide.md.
### A many-to-few remap silently destroys variety wherever one entity referenced two of the folded ids

Collapsing 5 Rohan crafted spears into 2 left `rohan_edoras_golden_hall_supreme_rider` with three
equipment rosters that previously offered two different spears and now offer the same one. Nothing
caught it: `validate_moduledata` passes because every `Item.X` still resolves, the polearm/shield
gate passes because the troop can still draw the weapon, and the data-flow trace passes because the
piece-to-item-to-troop chain genuinely is intact. The defect is not a broken reference, it is a
degenerate one.
- **Why missed:** every existing gate asks a RESOLUTION question ("does this id resolve", "can this
  troop use it"). None asks a COMPOSITION question ("does this entity now hold the same item
  twice"). Reference-level validation is structurally blind to it, so five of six review agents
  passed the change; only the agent that diffed per-troop instead of per-reference saw it.
- **Prevent:** for any migration that folds N ids into M < N, enumerate the affected entities and
  diff their id MULTISET before and after, not just the set of live references. Report any entity
  whose distinct-item count dropped. Do this before applying, because after the fact the
  pre-change composition only survives in a backup. And do not "fix" it by re-diversifying on the
  fly: substituting a different item to restore variety changes that entity's stats, which is a
  balance decision, not a data repair.
- **Source:** docs/reviews/rca-rohan-spear-reforge-2026-08-28.md finding 4.

### Read the mesh names out of the tpac; the FBX filename is not the id

Rohan's new spear art ships as `SM_Ro_Rohan_Spear_A.fbx`, and the tpac stores
`sm_ro_rohan_spear_blade_a`. Authoring the CamelCase form the artist hands you resolves to nothing,
silently: no error, no log line, the crafting piece simply never loads.
- **Why missed:** it wasn't, this time, but only because the workflow doc's Step C says to extract
  the authoritative strings from the `.tpac` and the extraction was actually run. The trap is that
  every human-facing artifact (the FBX, the asset folder, the artist's message) carries the
  CamelCase form, so the wrong string is the one in front of you.
- **Prevent:** `grep -aoE` the `_geo.tpac` for the mesh and `bo_` names and author from that output,
  every time. Related: TAOM crafting-piece ids use BOTH `wm_` (36) and `sm_` (22) prefixes and no
  tooling keys off either, so matching the tpac's prefix is safe.
- **Source:** docs/reviews/rca-rohan-spear-reforge-2026-08-28.md; docs/ai-includes/weapon-creation-workflow.md Step C.

### Before changing a reward threshold, enumerate what is attainable WITHOUT earning it
Lowering the band that pays a reward is a one-integer edit whose consequence lives in the scoring
function, not in the diff. Paying enlistment trust from the `solid` merit band (minScore 40) silently
started paying two populations that never fought: a player who quits the battle with a full kill
count banks 45, because the left-field penalty cancels only the survival term and kills, cohesion,
proximity, engagement and role fit all survive the exit; and a soldier who merely stands inside his
own line banks survival 25 + cohesion 15 = 40 with no kills and no engagement at all. Both reachable
in ordinary play, neither visible in a diff that changed one character.
- **Why missed:** the change was costed as "one integer, one band". The author verified the mechanism (does the reward now flow?) and not the population (who else does it now flow to?). Nothing in the reviewable artifact showed the attainable-score distribution.
- **Prevent:** for any threshold that gates a reward, write down the maximum score attainable by each way of NOT doing the thing the reward is for (quitting, standing still, dying early, auto-resolving) and assert the threshold sits above all of them. Pin it as a test that reads the SHIPPED config, and pin the positive side too (someone who does the thing must still clear it), or "no free reward" is satisfiable by paying nobody. Config comments recording such a ceiling are claims: re-derive the sum before trusting one, since the comment here reached its number by leaving kills out.
- **Source:** docs/reviews/rca-enlistment-standing-2026-08-28.md finding H1 (#520). Reproduced in play within three campaign hours of shipping: a zero-kill battle scored 46 with cohesion saturated and engagement near 0.2, landing inside the band that the reverted change would have had pay standing.

### A skill-check row that names only a specialist skill is unwinnable, not hard
Ten of thirteen enlistment field duties gated on Scouting, Charm, Steward or Tactics, which a warrior
hero carries at 0 for an entire campaign. Against a d51 roll, eight rows needed more than a natural
maximum: not punishing, impossible, and each attempt charged standing. The suite stayed green for
months because the reachability test assumed `UntrainedSkill = 10`, a number described in its own
comment as "roughly a fresh hero's untrained value" and which no log has ever produced.
- **Why missed:** the floor's assumption was authored alongside the rows it protects, so it flattered them. A constant that decides whether a test can fail is part of the test's claim and needs the same evidence as any other.
- **Prevent:** model the WEAKEST player a row's own gates admit, and for an untrained skill that is 0. Where a check should get easier with service, name a second skill the player provably accrues (in TAOM, `RunDailyTick` grants Leadership 10 XP/day to every enlisted player regardless of assignment) rather than lowering difficulty alone. Also assert a minimum pass probability: "passable at all" admits a 1-in-51 row, which in play is indistinguishable from a broken one.
- **Source:** docs/reviews/rca-enlistment-standing-2026-08-28.md findings M4 and the #438 root cause (#520).

### Cloning a culture's troop tree to promote it ships a duplicate faction, and reachability hides it
Promoting a kingdom out of a borrowed culture gave Blue Craig and the Misty Mountain Orcs a full
copy of the goblin tree each. Neither copy ever diverged. Blue Craig's differed from the source in
the `culture=` attribute and one display name; the Orc-host's added a race tag and different skill
numbers but kept the same 23 troops, the same upgrade graph and a byte-identical equipment list. In
play that is two kingdoms called "Goblins" fielding units a player cannot tell apart, and an
encyclopedia listing 69 near-identical goblin entries.
- **Why missed:** every gate that could have caught it measures reachability, not distinctness. `validate_moduledata.py` proved each ref resolved, `CulturePartyTemplateTests` proved each culture bound a TAOM template, and the volunteer-pool test proved every troop was recruitable. All three pass perfectly on a duplicate, because a duplicate is fully wired by construction. The one player-facing surface that would have shown it, the encyclopedia, walks `CharacterObject.All` and never asks whether two rows are the same unit twice.
- **Prevent:** when a clone script copies a troop tree, treat the copy as a debt with a due date, not as content. Before it ships, either author the divergence or point the new culture at the source tree, which is a supported and test-blessed pattern (Lothlorien fields Rivendell's whole, Umbar fields Harad's and keeps `umbar_elite`). A cheap standing check is to normalise the id prefix away and diff the troop files: 112 differing lines out of 2455 is not a second faction. Note that orphaning a duplicate is NOT enough to hide it, since the encyclopedia ignores reachability; the attribute for that is `is_hidden_encyclopedia`, not `hidden_in_encyclopedia`.
- **Source:** the 2026-08-29 goblin tree merge. Origin recorded in `tools/promote_borrowed_cultures.py`, which states plainly that both kingdoms previously ran on their host culture.

### A data rule derived from the cases in front of you is calibrated, not proven
One pass produced four instances of the same shape. A registry sync compared a trimmed value
against an untrimmed one, which is safe only if no value carries stray whitespace: six did, and
twelve languages lost real translations. "The translations already carry the accents, so an
accent-only English change needs no re-translation" was verified against Théoden and Éomer and
applied to Rhûn, Khand and Harad, where 23 of 28 keys were left permanently misspelled. A merge
model was inferred from a load-order fact rather than read out of the engine. A parse floor of
"> 1000" was picked against 1400 rows without asking which source contributes which rows, leaving
an entire stylesheet able to stop matching while the gate stayed green.
- **Why missed:** each rule was true of every case its author had looked at. Nothing prompted a
  check of the complement, and a passing result looks the same whether the rule holds everywhere or
  only on the sample.
- **Prevent:** when you derive a data rule from examples, write down the population you checked and
  then check the complement before generalising. If the rule encodes engine behaviour, decompile the
  engine instead of inferring from an adjacent fact. If it is a threshold, derive it per source so
  it cannot be satisfied by the wrong half of the data. Normalise both sides of any comparison, or
  neither.
- **Source:** the 2026-08-29 lord identity reconciliation, `docs/reviews/rca-lord-identity-2026-08-29.md`.

### A translation row that merely RESEMBLES the English is invisible to the pipeline forever
Four biographies were written into the twelve language files before a later step added diacritics
to the English, leaving `Grima Grimmoding` against a registry that had moved to `Gríma Grimmóding`.
The translator's discovery gate is `cur_text == eng_text`, so a near-miss is never staged again;
`--sync-ids` does not help because the key is present rather than missing; and the cache is keyed on
`string_id` alone, so a rebuild re-emits the same near-miss. The rows would have shipped as mangled
English in all twelve languages permanently, with no diagnostic anywhere and
`LanguageFileCoverageTests` green throughout, because presence is all it checks.
- **Why missed:** the rows were correct when written. They went stale because a later edit touched
  only the English and the registry. No gate looked for the near-miss, and the exact-match one
  cannot: an untranslated row is legitimately identical to its English.
- **Prevent:** when a reset writes English into a language file, copy the registry's BYTES rather
  than retyping the sentence, and do it after any pass that edits the English. `AccentStrippedTranslationTests`
  now fails on any row that is a diacritic-fold of its English, which a real translation never is.
- **Source:** the same pass. Related: RCA #388, the cache serving old wording back.

### An XSLT template that does not strip an attribute inherits vanilla's, and no repo test can see it
`heroes.xslt` overlays vanilla `heroes.xml` with `<xsl:copy>` plus an `@*[local-name() != ...]`
filter. Every attribute the filter does not name is copied from vanilla, and where TAOM has reused
a vanilla id for a different character, vanilla's value describes somebody else. Shipped result:
Gríma Wormtongue married to Éowyn with three children by her, Erkenbrand married to his own bearded
son, Duilin married to the woman the same template calls his mother, and three of Grimbold's
children with a female father and a male mother.
- **Why missed:** the inherited value exists nowhere in the repo, so a test that reads the markup is
  looking at a file that does not contain the bug. A first pass of gates built exactly that way went
  green on all six family invariants while eight of these were still live.
- **Prevent:** for data that reaches the game through a transform, assert on the transform's OUTPUT.
  `LordFamilyTransformTests` runs both stylesheets over the real vanilla documents with
  `XslCompiledTransform` and checks the graph the engine computes, skipping where the game is not
  installed. The authoring rule that goes with it: a template that assigns `spouse`, `father` or
  `mother` to a reused vanilla id must strip every family attribute it does not itself set.
- **Source:** the 2026-08-29 lord identity reconciliation. `docs/features/lord-identity-reconciliation.md`.

### A regex that pins attribute order silently halves a data gate's reach
`LordNameAndSexConsistencyTests` matched `<NPCCharacter id="..." name="{=...}..."`, which requires
`id` first and `name` second. Of 1184 entries in `characters/lords.xml`, 584 matched. The 600 it
skipped are the ones written `id="..." race="..." name="..."`, which is the entire Dol Guldur,
Isengard and Mordor roster: every orc and uruk in the mod. The gate had been reporting zero drift
across a population it was not reading.
- **Why missed:** a passing data gate looks identical whether it read everything or half of
  everything. Nothing prints the denominator.
- **Prevent:** parse attributes order-independently, and assert the population size the parse found
  (`Assert.IsTrue(lords.Count > 1000)`) so a shape change fails loudly instead of quietly shrinking
  the sample. Worth auditing any other data test whose regex bakes in attribute order.
- **Source:** the 2026-08-29 lord identity reconciliation.

### The same character authored twice, once with a name and once with a biography
Five Rohirrim (Théodwyn, Éowyn, Elfgrim, Herubrand, Siegeberht) existed as correctly named
`<NPCCharacter>` entries AND as biographies attached to entirely different vanilla ids. Three of the
correctly named ones had no `<Hero>` entry, so they never entered a campaign at all: Éowyn Eoforing
is fully authored, fully equipped, and has never once existed in play. The same shape turned up in
Isengard (Zorlag, Rukthar, Drûgash) and for a second lord named Duilin.
- **Why missed:** the two halves are separate files with separate authors and no cross-check, and a
  lord with no `<Hero>` entry produces no error, no warning and no encyclopedia row. It is invisible
  rather than wrong.
- **Prevent:** gate that every lord either has a `<Hero>` entry or is on a named exclusion list, and
  make the list self-expiring (a second assertion fails when an exclusion stops being needed). When
  a biography names somebody the data does not, search the roster for that name before renaming
  anything: the character usually already exists, and reattaching the prose keeps the twelve
  translations as a key rename instead of throwing them away.
- **Source:** the 2026-08-29 lord identity reconciliation, which added 36 missing `<Hero>` entries
  and left 23 excluded for want of a derivable clan.

### A pool-composition tool that never filtered mercenaries was hidden by pool ordering
`tools/build_clan_specs.py` excluded `_boss` troops from the roster pool but not `_merc` ones, so
tavern mercenaries were always eligible for a lord's party template. No shipped spec contained one,
which read as evidence the filter was unnecessary. It was luck: the composer draws by group and
band, and the merc leaf simply never won a slot. Pointing one culture at another culture's pool
shifted the ordering and two clan rosters immediately picked up `goblin_fighter_merc`.
- **Why missed:** absence of the bug in output was mistaken for absence of the bug. A latent filter gap in a seeded, order-sensitive generator produces clean output until an unrelated input change perturbs the order.
- **Prevent:** when a generator has an exclusion list, check it against the definitive list of things that should be excluded rather than against its current output. Here the authoritative list already existed in a test: `VolunteerRecruitmentServiceTests.IsIntentionallyUnrecruited` names `_militia_`, `_boss` and `_merc`, and the tool knew about only one of the three.
- **Source:** the 2026-08-29 goblin tree merge, caught by `generate_clan_heraldry.py`'s own drift gate refusing to regenerate.

### A name substring stood in for a binding, and the one false positive was the worst troop in the game
`rebalance_troops.is_militia` decided militia by name (`militia` plus spearman/archer/veteran).
Militia deliberately take the level-21 baseline whatever their real level, so a false positive does
not produce a small error, it produces a level-11 troop wearing level-21 stats.
`gondor_ano_archer_militia` was that troop: a plain Anorien line unit, never bound as a culture
militia troop, which then out-statted its own level-16 upgrade target on seven of its eight skills, -145 total
(Riding was already equal on both).
One false positive across 871 troops, and it was the single worst upgrade edge in the mod.
- **Why missed:** the heuristic was right 60 times out of 61, which is exactly the hit rate that
  stops anyone re-examining it. The authoritative list already existed one file away, as the
  `militia_troop` / `melee_militia_troop` / `ranged_militia_troop` bindings in `taom_spcultures.xml`
  and `spcultures.xslt`.
- **Prevent:** when a rule keys off a category, read the file that DEFINES the category. A name is a
  label an author types; a binding is a fact the engine consumes. This is the third time (#340, #341,
  #522): crossbowmen named "Sharpshooter", two-handers named "Knight", and now a line troop named
  "Militia". Assert the derived set's size so a rename fails loudly instead of widening the match.
- **Source:** #522, the 2026-08-30 troop upgrade regression fix.

### A gate that excludes the category the bug lives in reports zero forever
`analyze_troop_balance.check_monotonicity` excluded every militia troop, because militia
out-statting their level is intentional. That exclusion is also where the mis-detected militia sat,
so the check printed "0 inversions" for two months with a -145 upgrade edge inside its own blind
spot. Running the fixed check against the unchanged pre-fix data names the bug on the first run.
- **Why missed:** the exclusion was written for a real reason and documented as intentional, so
  every later reader accepted it. Nothing distinguishes "excluded because it is fine" from
  "excluded, and therefore never examined".
- **Prevent:** exempt the EDGE, not the entity. Militia-to-militia is genuinely flat by design;
  a militia that feeds a real line is an ordinary upgrade and must be checked. Whenever an exemption
  is added, ask what a bug hiding inside it would look like, and whether anything else would catch it.
- **Source:** #522.

### default_group is data the curve trusts, and nothing checked it against the equipment
`dg_warg_red_fang` was tagged `HorseArcher` while carrying sword, halberd and shield and no ranged
weapon at all, so the skill curve handed it Bow 240. Its parent and its child are both `Cavalry`
with Bow 25 and 40, which is where the -200 drop came from. It was the only HorseArcher in the mod
carrying nothing ranged.
- **Why missed:** `default_group` drives the battlefield formation AND the skill curve, but the two
  consumers never compare notes, and the troop looked correct in play because the engine picks the
  formation from equipment for the cases that matter. The wrong number was only visible in the
  encyclopedia.
- **Prevent:** the registry that answers this already exists. `taom_schema.build_item_class_registry`
  plus `rebalance_troops.troop_weapon_classes` will tell you what a troop actually carries in one
  call; a Ranged or HorseArcher troop carrying no Bow, Crossbow or Throwing item is worth a look
  (five javelin skirmishers are legitimate, so it is a review prompt, not an error).
- **Source:** #522.

### A missing dictionary key rebaselines a whole faction and raises no error
Lindon was the only culture file with no `CULTURAL_MODS` entry, so the formula ran against it with a
zero modifier. 27 of its 30 troops have a Rivendell twin carrying identical skill values, so the first
`--apply` after that gap opened would have stripped the high-elf tuning off the entire faction, in a
commit whose stated purpose was something else entirely. It surfaced only because a diff that should
have been 30 lines came out at 478.
- **Why missed:** `dict.get(key, {})` is the normal way to write an optional modifier, and it makes
  "this culture has no identity" and "somebody forgot to add this culture" the same value.
- **Prevent:** assert that every culture file resolves to a key that exists. Read the diff size per
  file before accepting a data-tool run: a file that moves an order of magnitude more than the tool
  said it would is the tool doing something you did not ask for.
- **Source:** #522, caught mid-apply and reverted before it shipped.

### A rebaseline tool with no clamp-only mode turns a bug fix into a roster-wide rebalance
The fix for the upgrade regressions needed one thing from `rebalance_troops.py`: raise a target to
its source. The only write mode was `--apply`, which recomputes the whole curve first, so the run
also rebaselined about 40 deliberately off-curve troops that had nothing to do with the bug: the
`gondor_loss_noble` line the doc explicitly says not to apply over (#343), the hand-authored Black
Numenoreans, the dwarf ram riders, and `mistymountainorcs_bolgs_ironfang`. All of it would have
shipped inside a commit titled "fix upgrade regressions".
- **Why missed:** the tool did exactly what it documents. Nothing was broken; the mode was simply
  wider than the task, and "run the tool" felt like the whole job.
- **Prevent:** before accepting a data-tool run, count the troops whose values went DOWN and name
  every one of them. A fix that only ever raises should have a lowered-set you can list on one line
  (here: the two deliberately reclassified troops). Split the mode when the tool cannot express
  the narrower intent: `--fix-monotonicity` now clamps from what is on disk, `--apply` still
  rebaselines, and `--restat <id>` handles the specific troops that genuinely need the curve.
- **Source:** #522, caught during self-review by diffing troop-by-troop against `HEAD` rather than
  trusting the tool's own change counter.

### skill_template makes a character's inline <skills> block unreachable, and every TAOM tool read the dead half
`BasicCharacterObject.Deserialize` (v1.4.8, `BasicCharacterObject.cs:337-358`) resolves
`skill_template` first and only calls `DefaultCharacterSkills.Init(childNode)` when that reference
came back **null**. So a character declaring both is asserting two different skill sets and the
engine silently takes the template. 44 militia troops shipped that way, pointing at vanilla
Calradian SkillSets: `rivendell_militia_spearman` was authored at 850 total and delivered 215. The
whole "militia take the level-21 baseline so sieges stay costly" doctrine was inert for every one
of them, and 17 prison guards had the same shape.
- **Why missed:** nothing in the pipeline knows the attribute exists. `rebalance_troops.py` writes
  the inline block, `analyze_troop_balance.py` reports it as the troop's actual skills, the feature
  doc documents it as live behaviour, and the schema has no rule that the two fields conflict. An
  earlier exploration did notice the pairing and recorded it as "vestigial `skill_template`
  attributes... worth confirming which wins". It wins.
- **Prevent:** `SKILL_TEMPLATE_SHADOWS_SKILLS` now errors on any character declaring both, in the
  validator and in `TroopUpgradeSkillMonotonicityTests`. Generalisation: when two fields can both
  supply the same value, find the engine's precedence rule and gate the contradiction, because a
  silent winner means every tool downstream can be reading the loser. "Vestigial" is a hypothesis,
  not a finding.
- **Source:** #523, found while widening the #522 gate to cover upgrade sources outside `troops/`.

### A gate scoped to where the bug was found inherits the bug's blind spot
The #522 gates were globbed to `troops/troops_*.xml`, because that is where the reported regression
was. 16 upgrade edges start outside it, in `characters/npcs_*.xml`, where each `villager_<culture>`
upgrades into its culture's tier-1 troop. Six of those edges regressed, and `validate_moduledata.py`
reported PASS the whole time. The engine treats any character with a non-empty `UpgradeTargets`
array as upgradeable, so they are real edges, not a technicality.
- **Why missed:** the scope was chosen from the symptom rather than from the definition. "Where do
  upgrade edges live?" and "where did this bug happen?" have different answers, and only the first
  one bounds a gate correctly.
- **Prevent:** derive a gate's scope from the invariant it enforces, then verify the scope by
  counting the thing being gated (698 edges across two directories, not 682 across one). A bare
  filename glob with no floor assertion is the shape to distrust: it silently covers nothing when
  a file is renamed.
- **Source:** #522 Codex review, confirmed by re-deriving the edge count from both directories.

### A gate written FOR a defect must state that defect's negation as a positive requirement (#525, 2026-09-01)

The enlistment kit shipped with no weapons. The fix rebuilt it and rewrote the coverage gate with
six new rules, every one of them a prohibition: slot allowlist, no mounts, no Item4, no duplicate
id, no empty roster, no shield on a support kit. It also gained per-assignment content rules, which
look like requirements but are conditional ("an `_archer_` roster must carry a bow") and so are
vacuous on a roster carrying no weapon at all. The fix then shipped 15 rosters with armour and zero
weapons, and the suite, both gates and `validate_moduledata.py` all passed on them.
- **Why missed:** the census run during implementation asked whether forbidden slots were present
  (Horse, Item4: zero, correct) and never asked whether required ones were. Prohibitions cannot
  express a feature's purpose, so a gate made only of them cannot fail on the absence of the thing
  it exists to guarantee.
- **Prevent:** the FIRST rule in a gate written for a defect is that defect's negation, stated
  positively, before any refinement. Here: "every roster carries at least one weapon-classed item".
  Pin it where `dotnet test` runs as well as in the tool, because nothing runs the Python gates
  automatically and this repo's CI compiles no C# either (the build job is gated on an unset var).
- **Sharper still:** a present-but-empty record can be WORSE than a missing one when a consumer
  probes existence. `EnlistmentRosterResolver` returns the first id that exists, so a weaponless
  roster ended the fallback walk and shadowed the armed kit the player would have descended to.
  Suppressing the cell was a one-line change and made the whole class unreachable.
- **Source:** #525; RCA `docs/reviews/rca-enlistment-weapons-2026-09-01.md`.

### Per-roster checks cannot see a progression that does not progress (#525, 2026-09-01)

Every enlistment gate and test examined one roster at a time. Nothing compared two. So 18
(culture, assignment) chains emitted a byte-identical kit at two or more ranks (bluecraig and
mistymountainorcs at all four), and 17 chains issued strictly WORSE armour on promotion, Erebor
infantry dropping 176 to 99 at the very first one. The ledger spends one draw per rank, so each of
those is a wasted promotion that hands the player duplicates of what he is already wearing.
- **Why missed:** the artefact is per-roster, the schema is per-roster, and so every check written
  against it was per-roster. A chain that does not progress is indistinguishable from one that does
  unless something holds two cells side by side.
- **Prevent:** when generated data encodes a SEQUENCE (ranks, tiers, levels), at least one gate must
  be cross-cell. Assert the sequence's defining property directly: distinct at each step, and
  monotonic in whatever the steps are supposed to improve. Donor trees are not monotonic in armour,
  so the generator cannot inherit the property from its inputs.
- **Source:** #525; same RCA.

### `_slim` is the slim-BUILD suffix, not the female one, and the engine appends it for you (2026-09-01)

`BasicCharacterTableau.cs:531-537` on v1.4.8 resolves an armour mesh like this:

```csharp
bool flag3 = flag && _equipmentHasGenderVariations[i];        // isFemale && has_gender_variations
MetaMesh val4 = MetaMesh.GetCopy(flag3 ? (text + "_female") : (text + "_male"), false, true);
if (val4 == null) {
    text2 = ((!flag3) ? (text2 + (flag2 ? "_slim" : ""))      // flag2 = slim BUILD
                      : (text2 + (flag2 ? "_converted_slim" : "_converted")));
```

Two consequences that are easy to get backwards, and both were got backwards on 2026-09-01:

1. **`_slim` is on the NON-female branch.** It is the body-build variant. The female suffixes are
   `_female`, `_converted` and `_converted_slim`, and only those are gated on
   `has_gender_variations`. "This mesh ships a `_slim`, so the gender flag should be on" is a false
   inference; acting on it set a flag that made a female fall through to the bare mesh instead of
   the slim one, which is strictly worse.
2. **The engine appends `_slim` itself**, so a hand-authored second item whose mesh is literally
   `<base>_slim` duplicates what you get for free. Thirteen such items existed in the Armory and
   were deleted; all had zero consumers, because nothing needs to equip them.

Measured across all 2,938 Armory armour items: **zero** have a `_female`, `_converted` or
`_converted_slim` mesh. TAOM has no female armour art and females are meant to wear the male art.
That makes `has_gender_variations="true"` strictly WORSE than `"false"` here, because `true`
sends a female down a branch with no art and she falls through to the bare mesh, while `false`
puts her on the branch that can still find `_slim`. Males are unaffected either way.

**The engine default is `true`** (`ArmorComponent.cs:159` sets it before checking for the
attribute), so an item that merely OMITS the flag also takes the dead female path. 28 items
declared `true` explicitly and 1,209 relied on the default. Only the 28 were changed: all 1,209
omitted items have no `_slim` to reach, so both settings end at the same bare mesh and adding the
attribute would be 1,209 edits for no behavioural difference. 22 of the 28 did have an
unreachable `_slim`, which is the whole of what the flip bought. Gate:
`tools/audit_gender_variation_flags.py`, which exits 1 while any item skips a reachable `_slim`.

- **Why missed:** the mechanism was inferred from the attribute's NAME plus a neighbouring item that
  set the flag the same way. Three plausible facts assembled into a conclusion nobody had read the
  consuming code for, and a review agent independently made the same inference, which felt like
  corroboration and was not.
- **Prevent:** before trusting an attribute to carry a mesh re-point, read the engine code that
  consumes it. A sibling row agreeing with you is not evidence; it may be repeating the same guess.
  The `Verify Before Reference` rule already covers this, and it is easy to apply it to mesh NAMES
  while skipping it for mesh SEMANTICS.
- **Source:** `docs/reviews/rca-armoury-dead-mesh-wave2-2026-09-01.md`.

### Re-pointing a `<CraftingPiece>` mesh without its `length` changes the weapon's REACH (2026-09-01)

Six `easterling_*` crafting pieces were re-pointed at surviving meshes and kept `length` and
`<BuildData>` tuned to the old geometry (spear handle declared 138 against a mesh whose canonical
piece declares 203). This was first recorded as cosmetic joint-misalignment, which understated it:
`WeaponDesign.CalculatePivotDistances` turns `length` into `CraftedWeaponLength`, and
`CraftingStats.FillWeapon` rounds that into the weapon's live combat reach. The visible mesh extent
and the hitbox are the same quantity, so a stale `length` produces a weapon that looks longer than
it hits, and attacks that appear to land can whiff.

No crash risk: the pivot maths is float arithmetic over a fixed-size array keyed on the piece-type
enum, so a stale length cannot throw or index out of range.

- **Why missed:** confirming the new mesh EXISTS felt like completing the re-point. It is only the
  first half; the attributes describing the old geometry are still there afterwards.
- **Prevent:** on a `<CraftingPiece>` re-point, treat `length` and `<BuildData>` as part of the
  change and decide explicitly. They are simultaneously art positioning and a gameplay stat, so it
  is a trade rather than a correction, and it belongs to whoever owns the balance.
- **Source:** `docs/reviews/rca-armoury-dead-mesh-wave2-2026-09-01.md`.

### An ORPHAN verdict is only as wide as the reference shapes the auditor knows (2026-09-01)

`audit_deleted_mesh_impact.py` classified all six `easterling_*` crafting pieces as ORPHAN, meaning
safe to delete. They are referenced by `<UsablePiece piece_id="x"/>` in `crafting_templates.xslt`
and by `<Piece id="x"/>` inside `<CraftedItem>`, and they build `easterling_sword` and
`easterling_spear`. `easterling_spear` is player career starting equipment, so acting on the verdict
would have deleted a Rhun start's weapon while every validator stayed green.

- **Why missed:** the matcher is documented as "attribute-agnostic by design", which reads as
  complete. It is agnostic about the ATTRIBUTE but not about the NAMESPACE: its pattern is
  `="Item\.(...)"`, and a crafting piece id is not an `Item.`. A tool that reports a clean negative
  for a shape it cannot express is more dangerous than one that reports nothing.
- **Prevent:** before trusting any "nothing references this" verdict, enumerate the reference shapes
  the tool actually matches and confirm the entity you are deleting can even be expressed in one of
  them. Both shapes are now a first-class `crafting_piece` ref kind with tests stating why they
  exist. Note the consumers live in the ARMORY tree, not the consumer root, so the sweep has to
  cover both roots.
- **Source:** `docs/reviews/rca-armoury-dead-mesh-wave2-2026-09-01.md`.

### Two agents agreeing is not corroboration when they read the same source (2026-09-01)

Two independent review agents concluded that deleting item definitions silently corrupts existing
saves, because `EquipmentElement`/`ItemRosterElement` implement `ISerializableObject.DeserializeFrom`,
which reads a raw `MBGUID` whose `SubId` is a sequential counter assigned in XML document order.
The reasoning was internally sound and the code they quoted is real. It is not the campaign save
path: `TaleWorlds.SaveSystem` references `ISerializableObject` zero times and the `Saveable*`
machinery 271 times, and `ItemObject` is registered as `AddClassDefinition(typeof(ItemObject), 32)`
and serialised through the save's own object graph.

- **Why missed:** agreement between agents was treated as evidence. It is not, when the agents read
  the same file. `evidence-over-claims.md` §A tells you to re-run the decompiler when agents
  DISAGREE; the mirror case, where they agree and are both wrong, has the same remedy and no rule
  pointing at it.
- **Prevent:** for any finding severe enough to change a decision, check whether the agents' evidence
  is actually independent, not just their reasoning. The tell here was that both quoted the same
  method. Independence has to be in the evidence.
- **Source:** `docs/reviews/rca-armoury-dead-mesh-wave2-2026-09-01.md`.

### A troop's equipment sets are a per-slot menu, not a set of alternatives (2026-09-01)

Players reported Lindon and Noldor recruits carrying a bow with no arrows, or arrows with no bow.
Every equipment set on those troops was individually valid. `Equipment.GetRandomEquipmentElements`
fills each of the 12 slots from an independently chosen set, so 3 ranged sets among 13 produced a
working archer 5% of the time and a useless half-kit 36% of the time. There is no set-count
threshold in the method; the widely believed "more than 3 sets" trigger does not exist, and mixing
begins at two sets.

- **Why missed:** every validator TAOM owns asks "is this set valid?" and the answer was yes for all
  13. The engine never asks that question. It asks "what is in slot 0?" thirteen times. No gate, and
  no reviewer, was framing the invariant per slot across sets. The defect is also invisible in game
  by construction: encyclopedia, party screen, troop tree and tournament all use whole-set selection
  and render set #1, so the only surface that shows it is a live mission agent.
- **Prevent:** validate a troop's battle sets as a **column-wise** family, not row by row. For each
  slot index, the classes appearing across sets must be compatible: if any set can put a launcher in
  a slot, all must, and the ammo needs one fixed index present in every set. At least one index must
  hold a weapon in every set, or a draw can produce a troop with nothing. Recorded as an authoring
  rule in `.claude/rules/troops.md`, which is path-scoped to the troop XMLs and so loads for exactly
  the edit that would reintroduce this. Corollary caught the same day: the identical cross-set shape
  defeats `audit_polearm_shield_parity.py`, which is per-set, so 14 troops can draw a shield against
  a `requires_no_shield` weapon with neither set malformed and the gate exits 0 (#531).
- **Source:** `docs/reviews/rca-troop-equipment-slot-mixing-2026-09-01.md`, #529, #531.

### A "CLEAN" result describes the predicate, not the data (2026-09-01)

The sweep written to prove the fix above reported CLEAN. It was checking four predicates over five
weapon slots, and the review found it could not see Horse/harness pairing, cross-set shield
conflicts, armour, or any roster outside its own glob. Worse, it called
`build_item_class_registry`, which silently returns an empty dict when the game install is absent:
with no items classified, every check finds nothing and the tool prints CLEAN and exits 0.
Reproduced with one environment variable.

- **Why missed:** the tool printed no evidence of its own reach. It reported findings but never the
  size of the registry it classified with, so "1,463 items, genuinely clean" and "0 items, saw
  nothing" produced identical output. `_gamedir.ensure_exists` exists precisely for this and was not
  called.
- **Prevent:** a gate must print what it examined, not only what it found, and must refuse to report
  when its inputs are empty. Any tool resolving the install goes through `ensure_exists`, and any
  registry-backed check asserts the registry is non-empty before drawing a conclusion. State a clean
  result as "no instances of these N predicates", never as "the data is clean".
- **Source:** `docs/reviews/rca-troop-equipment-slot-mixing-2026-09-01.md`.

### Two validators see disjoint halves of an art incident, and each looks clean alone (2026-09-01)

An artist reorganisation deleted both art that XML named and item definitions that troops equipped.
`validate_mesh_refs.py` reported 63 errors. `validate_moduledata.py` reported 212. Neither number
was the damage; the damage was 275, and the two tools cannot see each other's half by construction:
one resolves mesh names against the asset tree, the other resolves item ids against the registry.
An item whose ART vanished is invisible to the second; an item that vanished ENTIRELY is invisible
to the first, because there is no longer an item to walk.

- **Why missed:** the mesh gate was the one that had just been fixed and was front of mind, so its
  63 read as the incident. The 212 were found only because a review agent ran the other tool
  unprompted. Nothing in the repo said to run both after an art drop.
- **Prevent:** after any change to the Armoury, run BOTH. A clean mesh gate says nothing about
  dangling item references and vice versa. Better, run `generate_armory_catalogue.py --diff`, which
  is the only artifact that sees the change as a change rather than as a symptom.
- **Source:** `docs/reviews/rca-armoury-keyforce-cleanup-2026-09-01.md`.

### Prove when art went missing before calling it a deletion (2026-09-01)

24 Gondor sword meshes were missing and two independent review agents both attributed them to the
artist cleanup that had just landed, one calling them a deletion and the other implicitly excluding
them. Scanning all 4,287 tpacs at the base commit settled it: the only Gondor sword art that has
EVER been in the assets repo is one tpac holding four variants. The cleanup moved that file and lost
nothing. The XML had named ten variants while four shipped, for as long as the repo has existed.

This matters because the remedy differs completely. Art the cleanup deleted is recoverable with
`git cat-file --filters <base>:<path>`, one command. Art that never existed cannot be restored at
all, and the only options are to re-author it or to rebuild the items from what survives.

- **Why missed:** a mesh missing today and a commit landing today is a compelling coincidence, and
  both agents took it. Neither checked the base commit.
- **Prevent:** before attributing missing art to a commit, confirm it existed at that commit's
  parent. For an LFS repo that means `git cat-file --filters`, never `git show`, which returns a
  ~130-byte pointer that scans to zero meshes and is indistinguishable from a genuinely empty
  result.
- **Source:** same RCA.

### Rebuild a crafted item from surviving PIECES, never by re-meshing a piece (2026-09-01)

Six crafted swords had no art. They were rebuilt by changing which `<Piece>` each `<CraftedItem>`
names, drawing from the four surviving variants: 16 parts give 256 combinations, so each rebuilt
sword is genuinely distinct rather than a duplicate.

This is the safe direction of a defect made earlier the same day, and the distinction is worth
holding onto. Changing a PIECE's `mesh=` leaves that piece's `length` and `<BuildData>` describing
the old geometry, and `length` becomes the weapon's live combat reach, so the weapon ends up looking
longer than it hits. Changing which piece an ITEM names keeps every piece internally consistent and
lets the engine recompute reach from the parts chosen. A collision body also comes from the blade
piece, so choosing a surviving blade fixes the body for free.

- **Prevent:** when art is missing under a crafted item, re-point at the item level. Re-mesh a piece
  only when you are also willing to re-derive its geometry, and treat that as a balance decision
  rather than a repair.
- **Source:** same RCA.

### Rebuilding a crafted weapon from surviving parts is a balance change, not just a repair

Verified by decompile on 2026-09-01. Six Gondor swords were rebuilt from mixed surviving pieces
after their art turned out never to have shipped. The repair was correct and the reach numbers were
never stated, so a 31 cm loss on a weapon with 53 references would have shipped as a silent nerf.

Three engine facts decide what mixing costs, and none is guessable from the XML:

- **The pommel contributes nothing to reach.** `OneHandedSword` gives it build order `-1`, so
  `CalculatePivotDistances` never accumulates it into the length. Reach is
  `hilt/2 + (guard - prev_offset - next_offset) + blade`.
- **The blade alone decides collision body and swing damage.** `InitCraftedItemObject` reads
  `UsedPieces[0].CraftingPiece.BladeData`, and `_swingDamageFactor` comes from the same place. So a
  hotter surviving blade raises damage whether or not you intended it, and choosing any surviving
  blade fixes a missing collision body for free.
- **Only the guard carries non-zero `BuildData`.** Hilts and pommels are all zeroes here, so mixing
  is free at those seams and never free at the guard-to-blade one: each guard's `next_piece_offset`
  was authored for its own blade, and swapping moves where the blade seats by up to 6 cm.

- **Prevent:** when re-pointing an item's pieces, compute the before-and-after reach and swing
  damage and put them in the CHANGELOG. The arithmetic is short and the alternative is a stat change
  nobody can find later. The guard-to-blade seam is the one thing the arithmetic cannot answer;
  check it in the model viewer.
- **Source:** `docs/reviews/rca-armoury-keyforce-cleanup-2026-09-01.md`, engine detail in
  `docs/features/weapon-xml-pipeline.md`.

### A `<UsablePiece>` deletion unregisters the item; an `<AvailablePiece>` deletion does nothing

The two crafting stylesheets look like a matched pair and behave nothing alike, which matters
because a cleanup naturally edits one and forgets the other.

`crafting_templates.xslt` feeds `CraftingTemplate.Pieces`, and `GenerateCraftedItem` returns null
for any `<CraftedItem>` naming a piece that is not in it. `ItemObject.Deserialize` then calls
`MBObjectManager.UnregisterObject` and **the item ceases to exist**, so every troop and roster
naming it holds a broken reference. `weapon_descriptions.xslt` feeds `WeaponDescription.Deserialize`,
which resolves each id through `MBObjectManager.GetObject` and skips a null. Vanilla shows the
tolerance is real and rarely used: of 1,233 `<AvailablePiece>` ids in its `weapon_descriptions.xml`,
all 429 `mp_*` ones resolve and exactly one id dangles.

CLAUDE.md's shield trap row describes a third case, a weapon matching the *wrong* description, which
is the only one of the three where the item survives and misbehaves. For a template declaring a
single `<WeaponDescription>`, as `OneHandedSword` does, there is no wrong description to match: a
no-match is unregistration.

- **Prevent:** delete a piece from `crafting_templates.xslt` only together with every
  `<CraftedItem>` naming it, and run `validate_moduledata.py` afterwards, since the failure surfaces
  as `BROKEN_ITEM_REF` rather than anything weapon-shaped. Clean the sibling stylesheet in the same
  pass for hygiene, not for correctness.
- **Source:** same RCA.

### Reverting a balance knob does not revert the data that was tuned to it

The AI Party Size feature's multiplier was walked back 10.0 to 5.0 to 2.5 to 1.0 (neutral) over one day. The party TEMPLATES raised alongside it in the 2026-08-14 pass were never walked back with it: goblin lord templates still sum to 4500, orc and uruk 3500, men 1500. Vanilla spawn never consults `PartySizeLimit`, so at the neutral default an AI lord still spawns hundreds to thousands of men and is still shed to the vanilla 40-203 cap on the first daily tick, reproducing in full the exact symptom (#461) the feature was built to remove. Every doc, registry row and issue comment still described the raised behaviour as shipped.
- **Why missed:** the knob and the data live in different files, different formats and different features, and only the knob was in the changeset. A code review of the changeset cannot see data that did not change, and nothing links a template max to the cap model that trims it. The staleness was found only by asking "what does this feature now DO by default", not by reviewing the diff.
- **Prevent:** when a tuning knob moves far enough to change a feature's default posture (on to off, scaled to neutral), grep for the DATA that was authored against the old value and either move it too or write down why it stays. Then re-read the feature doc's own Overview as a stranger would: if it describes a capability the shipped defaults no longer deliver, the doc is wrong, not merely dated. Extend that sweep to registry rows (`feature-map.md`, `gamemodel-registry.md`), derived config (`startup_resources_config.xml`'s K), and any GitHub issue whose resolution comment asserted the old default.
- **RESOLVED the same day.** The templates were retargeted to 320/260/220/200/150 within hours of this being written (`151b6f56`, v2.0.27), matching the neutral cap. Doing it surfaced a second defect: the retarget's proportional scaling had no floor and zeroed 45 Mordor stacks, deleting six troop types, fixed in `bb01b9a4` (v2.0.28). See the entry below on proportional rescales. The lesson stands regardless of the fix, because the gap was found by asking what the feature does at its shipped defaults, not by reviewing the diff.
- **Source:** docs/reviews/rca-ai-party-size-player-clan-2026-09-01.md, tailored second pass (#530, #532).

### An `_a` / `_b` suffix pair is an art variant, not a tier

Measured 2026-09-01 while dressing Umbar from Mordor's Black Numenorean set.
`sm_md_num_inf_chest_heavy_b` is 85 total armour against `sm_md_num_inf_chest_heavy_a`'s
89 (`arm_armor` 26 vs 30). Promoting `_a` to `_b` across a tier boundary therefore
LOWERED armour on five upgrade edges, and a sixth was flat because `_med_a` and
`_med_b` are both 69. The tree looked like a clean ladder and cost the player armour
for a promotion.

The same set has three more plateaus that defeat name-based tiering: every hood and
helmet from `med` upward is 47, greaves are 41 at med, heavy AND elite alike, and
pauldrons are 45 at heavy and elite alike. Only Body and Cape actually climb.

- **Prevent:** sum the `<Armor>` attributes before assigning an item to a tier, and
  walk `<upgrade_targets>` asserting the total strictly increases. Never infer rank
  from a trailing letter or from `light/med/heavy/elite/lord` in the id. This is the
  existing "never select a game item by what its name implies" lesson recurring on
  a suffix rather than a display name.
- **Source:** `docs/reviews/lessons/data-content-cultures.md` sibling entries;
  tool `tools/apply_umbar_equipment.py`.

### A donor set has a floor, and the floor decides which tiers it can dress

A full five-slot Black Numenorean kit is **160 armour minimum**, because the lightest
piece in each slot is already elite-line art (Mordor fields it at levels 26 to 46). It
cannot dress a level-6 troop at cohort weight no matter which "light" variants are
chosen: the first Umbar pass put a level-6 bandit at 168 against a level-6 cohort
maximum of **71**, and landed the whole tree at the **100th percentile of its level
cohort at every single tier**.

The fix was not retuning within the set but splitting donors: Harad for the rank and
file, Black Numenorean for the level-31 capstone, the boss and the lords. That also
fixed a faction-identity problem for free, since 36 of 39 items had been shared with
Mordor's own line.

- **Prevent:** before adopting a donor set for a tree, sum its lightest legal kit and
  compare against the target's LOWEST tier, not its highest. If the floor exceeds the
  low-tier cohort median, the set cannot serve that tree alone. Check the percentile
  against same-level troops in other cultures both ways: too high is as much a defect
  as too low, and the too-high direction produces an early-game difficulty spike that
  no validator can see.
- **Source:** same session; the prior "item-tier correct and wearer-level correct are
  two different checks" lesson is the same failure pointing the other way.

### An index-keyed variant rotation is unstable and collapses variety

`idx % 4` over file order dressed 26 Umbar notables from 4 kits, replacing 18
distinct pre-existing looks with 4. Worse, the key was position, so inserting one
`<NPCCharacter>` at the top of the file re-dressed every notable below it.

- **Prevent:** key a variant rotation on a stable property of the entity, usually a
  hash of its own id (`zlib.crc32`, not `hash()`, which is salted per process and
  re-rolls every run). Count the DISTINCT looks before and after any bulk re-dress;
  a pass meant to add variety that reduces it is a regression the diff will not show.
- **Source:** same session.

### A proportional rescale deletes the thinnest rows, silently and permanently

`rebalance_party_template_maxes.py` scales each party-template stack's spread proportionally to hit an absolute per-culture target. Retargeting Mordor from 3500 to 260 rounded 45 stacks to `min 0 / max 0`, removing six Black Numenorean troop types from 14 of Mordor's 16 lord templates. A 0/0 stack is unreachable from both spawn paths: the initial fill draws `min + (max - min) * r`, and vanilla's new-game top-up weights each stack by `(min + max) / 2`. It is also unrecoverable by the tool, because the next retarget scales from a spread that is now zero and `0 * anything` stays 0 at any future target.
- **Why missed:** every gate passed. The tool reported success, the XML parsed, `validate_moduledata.py` returned 0 errors, and 8000 unit tests passed, because nothing anywhere asserts that a stack which could previously spawn a troop still can. The exposure was invisible from the target number alone: Mordor carries 52 stacks against the same budget every other culture spends on 12 to 27, so only Mordor's thinnest stacks (5 of 3500) crossed the rounding floor.
- **Prevent:** any proportional rescale of a distribution must floor every non-zero row at 1, and the tool must refuse to write a row that goes from "can occur" to "cannot occur". Then diff the SET of live entries before and after, not just the sums. A sum that lands exactly on target says nothing about what stopped existing to get there. Pinned by `tools/tests/test_rebalance_party_template_maxes.py`, whose first test fails against the pre-fix formula.
- **Source:** deep review of `151b6f56`, fixed in the follow-up; see CHANGELOG 2026-09-04.

### A level shift is a restat

`f9942d84` moved the ten `dg_uruk_*` troops up five levels and left their skills where they were, so every one of them carried the previous level's curve: an L36 Black Guard tied an L31 Khamul infantryman, and a L31 Khamul archer out-totalled the L36 Uruk sharpshooter by 145. Nothing on the edges went backwards, so the per-skill upgrade gate was clean, and the analyzer's within-culture sweep has a 25-point tolerance that the exact ties slipped under.
- **Why missed:** the gates guard EDGES, and a whole line shifting together keeps every edge monotone. The line was wrong relative to the curve, not relative to itself.
- **Prevent:** any change to a `level=` attribute is followed by `rebalance_troops.py --fix-monotonicity --restat <ids> --dry-run`, and the analyzer's outlier list (troops more than 50 off the formula) is read after a level move, not only the inversion list.
- **Source:** #541, CHANGELOG 2026-09-04.

### Equipment is a ladder the same way skills are

62 upgrade edges lowered the troop's armour total when first measured, up to 74 points, and no tool said so: the skill clamp and `UPGRADE_SKILL_REGRESSION` read `<skills>` only. Three shapes produced them: a line re-dressed by name into a lighter family (`plate_light` under `hplate_heavy`), a slot simply never filled (the uruk skirmisher had no gloves or cape), and the parent being the anomaly (a recruit in a lord's torso, two "medium" helmets carrying the lord row's value).
- **Why missed:** rosters were reviewed for looks and for broken refs, never for the number the engine adds up, and the pieces that hid longest were capes, whose body armour the primary-stat view never shows.
- **Prevent:** `UPGRADE_ARMOUR_REGRESSION` warns on any such edge; `fix_upgrade_armour_regressions.py` repairs it by stepping the target up its own item family. When the parent is the anomaly, demote the parent (the script's `DEMOTE`) or fix the item value; raising a whole tree to hero kit is the wrong fix.
- **Source:** #541, CHANGELOG 2026-09-04.

### A generator's variant axis must match the roster generator's

`generate_dale_armor.py` reads `a01..a04, b01..b04` as one eight-step tier ladder; `generate_dale_troops.py` uses `a`/`b` as the bronze/silver lines and `01..04` as the tiers. The two agreed on nothing, so the L21 Guardsman wore stats identical to the L16 Footman's and the Longbowman lost 21 armour on promotion, and a roster-anchored restat could not repair it while an L11 troop wore the mid variant.
- **Why missed:** each generator was internally consistent and validated against its own manifest; the seam between them was never stated in either.
- **Prevent:** when a culture's armour and troops come from two generators, write the suffix contract (which token is the line, which is the tier) once in the feature doc and point both generators at it. Before a roster-tiered restat, check who the LOWEST wearer of each mid item is: one low troop pins the whole item.
- **Source:** #541, CHANGELOG 2026-09-04.

### Retuning shared ModuleData: enumerate its CONSUMERS before you enumerate its values
A party template, equipment roster or culture list is a shared surface. Before changing the numbers
in one, grep every reader of the binding it hangs off (here `CultureObject.CaravanPartyTemplates`),
not just the reader the change is about. A size change is invisible to every existing gate: no
reference breaks, no file fails to parse, `validate_moduledata.py` stays clean, and the second
consumer simply starts behaving differently.
- **Why missed:** the changeset's own framing was "two halves of one change", the XML template maxima
  plus a paired C# member-cap raise. That phrasing asserts a closed system, and it was wrong.
  `SupplyLines` builds its player supply caravans from `culture.CaravanPartyTemplates[0]` but its
  `SupplyCaravanComponent` derives from `PartyComponent`, not `CaravanPartyComponent`, so
  `MobileParty.IsCaravan` is false and the paired cap raise could not reach it. Its escort went from
  20-29 to 60-70 troops and its provisioning cost, which is linear in headcount, went up with it.
  Five of the six review agents saw only correct files; the three files involved sit in two unrelated
  features. It surfaced only because the data-flow agent's prompt named SupplyLines as a suspicion.
- **Prevent:** for any `ModuleData` retune, list the entity's binding attribute, grep the whole repo
  for readers of that binding, and state what each one does with the value. Two readers is the normal
  case, not the exception. Also check the inverse: a consumer that is *not* the party type you think
  it is. `IsCaravan` is `_partyComponent is CaravanPartyComponent`, so a lookalike component built on
  the base class silently fails every `IsCaravan` gate, including the ones you are relying on to
  reach it.
- **Source:** docs/reviews/rca-caravan-bandit-parity-2026-09-06.md finding 1; issue #549.

### An ABSENT element has no reference to break, so every reference-based gate is blind to it
`validate_moduledata.py` is a cross-reference validator: it proves that `Item.x`, `NPCCharacter.y`,
`BodyProperty.z` resolve. That model can only see a reference that is present and wrong. An element
that was never written has no id to fail, so it passes every sweep, and if the engine's own XSD
declares that element optional, the editor passes it too. Whenever a required-in-practice child
element carries meaning, the gate has to be a test that asserts PRESENCE, not a validator rule that
asserts resolution.
- **Why missed:** 46 `NPCCharacter` entries across ten cultures shipped with no `<face>` block, the
  whole arena practice set. `BasicCharacterObject.Deserialize` then builds their `MBBodyProperty`
  from `default(BodyProperties)`, whose age is 0, and `skins.xml` maps age 0 to the toddler skin, so
  arena fighters spawned waist-high. `BROKEN_BODY_PROPERTY_REF` would have caught a typo'd
  `face_key_template` and had nothing to say about a missing one. `NPCCharacters.xsd` makes `<face>`
  optional, correctly, because vanilla has characters without one. And the engine's two age guards in
  `Mission.SpawnAgent` (force 29 at age 0, force 27 for a sub-teenager in a battle-like mission) read
  `CharacterObject.Age`, a different property clamped at deserialisation to `max(20f, ...)`, so a
  faceless character reports 20 and passes both while its visual age stays 0. Three guards, all
  keyed on something other than presence.
- **Prevent:** `TAOM.Tests/Core/CharacterFaceCoverageTests.cs` asserts every `NPCCharacter` under
  `Main/_Module/ModuleData` declares a `<face>`, with a floor assertion so a changed layout fails
  loudly rather than vacuously. More generally: when an authoring recipe produces the omission, fix
  the recipe in the same pass. All three "add a culture's practice dummy" recipes listed the id, the
  equipment roster and the item-id check and none listed the face, so ten cultures were authored
  correctly against an incomplete recipe while nine later ones happened to copy a good sibling.
- **Source:** docs/reviews/rca-arena-toddler-fighters-2026-09-06.md

### A documented gate limitation is a backlog item, not a disclaimer
`docs/features/moduledata-validation.md` carried the sentence "`is_female` has no rule at all" from
the 2026-08-29 lord RCA onward. It was accurate, it was prominent, and it was read as a description
of where the validator stops rather than as a list of what to go and check by hand. Nine days later
the same gap surfaced again, one directory over: 166 female-role entries across 17 cultures had no
`is_female="true"` at all and rendered as men, and all 596 notable templates were male. When you
write down that a gate cannot see something, you have just written a to-do; either close it or say
which unchecked files it leaves exposed.
- **Why missed:** the lord pass fixed `characters/lords.xml` and built
  `LordNameAndSexConsistencyTests` scoped to that one file. Nobody re-ran the question against the
  sibling `npcs_*.xml`, where the same class was present in the opposite direction: lords carried
  the wrong sex, townsfolk carried none. Nothing could catch it automatically, because
  `tools/schemas/taom_npccharacter.json` enumerates `default_group` and nothing else, and a missing
  `is_female` is a semantic defect: it needs a gate that knows the id `townswoman_gondor` implies a
  woman. Three things also made it look deliberate rather than broken. It fails silently with no
  log line; the clothing had already been made female by an earlier pass, so outfits said "woman"
  while bodies said "man"; and the culture wiring in `taom_spcultures.xml` was correct throughout,
  so auditing that layer found nothing.
- **Prevent:** `TAOM.Tests/Core/TownsfolkAndNotableSexConsistencyTests.cs`. More generally, when you
  fix a defect class in one file, enumerate that file's siblings and re-run the same question before
  closing the issue. One culture behaving correctly, as Rohan did here, reads as "the others need
  art" and hides the fact that one file got an attribute the other seventeen never did.
- **Source:** docs/reviews/rca-townsfolk-sex-2026-09-06.md
### Deleting a false positive is not the same as meeting the need it was faking
`rebalance_troops.py` once fired its Bow/Crossbow swap on the name keyword `naffatun`. That was wrong,
so #340 removed it, and the removal is recorded two entries above as part of a clean fix. Nothing
replaced it. The two Rhûn troops the keyword had been mis-serving carry two javelins and an axe, no bow
and no crossbow, and they spent the next two months with Bow 195 / Crossbow 160 / Throwing 55: every
ranged point on weapons they do not own, and almost none on the one they throw. Three Gondor Harondor
skirmishers sat in the same shape, and all five were additionally tagged `default_group="Ranged"`, so
the engine also fought them as backline archers while they held a javelin and a sword (#554).
- **Why missed:** three independent silencers, and it takes all three to hide something this visible on
  a troop card. `default_group` fails soft: `FetchDefaultFormationGroup` returns -1 rather than throwing,
  so a wrong value produces no log line. The wrong skill values were **inert**, which reads as harmless,
  and `docs/features/troop-skill-balance.md` had written the Rhûn case down as an accepted cost, which
  converts a defect into documentation and stops anyone re-asking. And `analyze_troop_balance.py`
  imports the same curve it audits, so a troop that is on-curve but wrong is invisible to it by
  construction, which is the SAME blindness the entry two above this one already recorded and fixed for
  a different symptom.
- **Prevent:** when you delete a heuristic for firing on the wrong rows, name the rows it was firing on
  correctly and say what now serves them. If the answer is "nothing", that is a backlog item, not a
  completed fix, exactly as "A documented gate limitation is a backlog item, not a disclaimer" says for
  gates. Second, treat "inert" as a description of a value, never as a verdict on a defect: a skill on a
  weapon the troop does not carry is only harmless if the weapon it DOES carry is served, and here the
  same sentence that called it inert was the evidence it was not. Third, a `default_group` that
  contradicts the carried equipment now has a written convention in `docs/modding/troops.md` and a
  planned gate (`THROWN_MELEE_MISGROUPED`, #555), because a soft-failing enum needs an explicit check or
  it has none at all.
- **Source:** #554; docs/reviews/rca-javelin-troop-misclassification-2026-09-06.md; #555 for the blocked
  systematic half.
