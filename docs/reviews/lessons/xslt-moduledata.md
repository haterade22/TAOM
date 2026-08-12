# Lessons — XSLT & ModuleData

> Category file of the master lessons record — index + house shape: [LESSONS-LEARNED.md](../LESSONS-LEARNED.md). **Append new XSLT & ModuleData lessons HERE** (`### rule` → `**Why missed:**` → `**Prevent:**` → `**Source:**`).

### Use SandBoxCore (not SandBox) as the vanilla XML reference
Always reference `SandBoxCore/ModuleData/` as the authoritative source for vanilla XML structure, NOT `SandBox/ModuleData/`. SandBoxCore uses the element names the engine actually reads (e.g. `<notable_templates>`), while SandBox uses different names (e.g. `<notable_and_wanderer_templates>`) the engine ignores — so the wrong source produces XSLT transforms that silently fail.
- **Prevent:** When writing/debugging XSLT, looking up vanilla element/attribute names, or cross-referencing culture defs, check `SandBoxCore` first.
- **Source:** memory/feedback_sandboxcore.md

### TAOM_Map settlements.xml is external and live; the repo copy is a stale shadow
The campaign-world settlements live in the **external** `TAOM_Map` module: `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml` (+ `Languages/<LANG>/loc_settlements.xml` ×12), registered via `TAOM_Map/SubModule.xml` → `<XmlName id="Settlements" path="settlements"/>`. The repo's `Main/_Module/ModuleData/settlements.xml` is a **stale shadow** (last touched 2026-04-06, commit `59348b9`), NOT registered in `Main/_Module/SubModule.xml`, and its position data has diverged — editing it does NOT affect in-game behavior. Settlement IDs (`<Settlement id="...">`) are save-bound — never rename; the `name="{=key}DEFAULT"` display attribute is safe to change.
- **Why missed:** Existing tools (`tools/Apply-SettlementNames.ps1`, `Generate-Settlements.ps1`, `Settlement-Breakdown.ps1`) all target the stale repo copy; running them only updates the orphan snapshot. Discovered 2026-05-26 during deep-review of the village-placeholder rename pass.
- **Prevent:** For live display-name/data changes, edit the EXTERNAL `TAOM_Map/ModuleData/settlements.xml` directly (or via `tools/Apply-MapVillageNames.py`) and mirror across the 12 `loc_settlements.xml` files. To verify which copy is loaded, grep `Main/_Module/SubModule.xml` for `XmlName id="Settlements"` — no match means the repo copy is shadow.
- **Source:** memory/feedback_taom_map_live_vs_stale_shadow.md + docs/reference/taom-map-settlement-naming.md

### Grep `name="X"` (not `X=`) in XSLT, and verify the EFFECTIVE (last) value
When hand-editing a TAOM `*.xslt` transform (`lords.xslt`, `heroes.xslt`, `spclans.xslt`, …), two silent failure modes no-op your edit with NO error and NO crash. **(1)** XSLT sets attributes via `<xsl:attribute name="X">VALUE</xsl:attribute>`, NOT `X="VALUE"` — so `grep "race="` returns ZERO matches even when many templates set race; always grep `name="X"` (e.g. `name="race"`). **(2)** A duplicate `<xsl:attribute name="race">` → .NET `XslCompiledTransform` emits the LAST one (your added line is discarded). **(3)** A duplicate `<xsl:template match="[same id]">` (e.g. two `Hero[@id='lord_1_56_2']`) → last template in document order wins; if it doesn't set the attribute you added, your override is dropped. An XSLT edit is verified only by confirming the EFFECTIVE (last) value — count occurrences (`name="race"` once per template, one `xsl:template match` per id), not that your new line is present.
- **Why missed:** Bit twice in one session (2026-06-22): manual `race=` edits added `race=uruk`/`race=orc` AFTER the id line on `lord_1_27` (Maugrash) and `lord_1_63` (Gorbag) which already had the opposite race set lower in the template → engine took the stale value, inverting both; a new `heroes.xslt` `lord_1_56_2` template (Rustica clan move) duplicated a pre-existing one at line 1252 → last-wins discarded the `faction` override. A regex extractor using `re.search`-first masked #2 by reading the FIRST attribute. A 4-agent adversarial verification workflow caught both.
- **Prevent:** Before adding any `<xsl:attribute>` / `<xsl:template>`, grep `name="X"` + the id and edit-in-place (or delete the stale one) — never add a duplicate. Prefer replace-within-block scripts (`tools/deconflict_lord_cultures.py`, `reculture_lords.py`, `retheme_groupa_dunland.py`) which replace within a template block and can't create duplicates.
- **Source:** memory/feedback_xslt_attribute_and_template_duplicates_silent_lastwins.md

### Civilian rosters in standalone EquipmentRosters files need `equipmentType="Civilian"`
When authoring/editing a standalone `<EquipmentRosters>` file (`Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_*.xml`, mirroring vanilla `SandBoxCore/ModuleData/sandboxcore_equipment_sets.xml`), every civilian roster MUST carry `equipmentType="Civilian"` on the inner `<EquipmentSet>`. Vanilla `BasicCharacterObject.Deserialize` (verified v1.3.15) keys classification off the `equipmentType` attribute, NOT the roster ID — battle is the implicit default (there is NO `equipmentType="Battle"` in vanilla; zero matches across SandBoxCore). A civilian roster missing the attribute renders battle gear in encyclopedia portraits, dialog scenes, and settlement-walk views, and competes with battle rosters at spawn. (Inline equipment under `<NPCCharacter><Equipments>` uses a different attribute, `civilian="true"` on `<EquipmentRoster>` — a separate pattern.)
- **Why missed:** 2026-05-23 — Faramir rendered as a peasant in his encyclopedia portrait while Boromir rendered correctly only by coincidence (his civilian/battle rosters used near-identical heavy plate, so the misclassification was invisible). 96 civilian rosters across all 16 culture files were missing the attribute (fixed in commit `5ae0a2a`).
- **Prevent:** Before commit, grep `<EquipmentRoster id="[^"]*_civ` and verify the next `<EquipmentSet>` line has `equipmentType="Civilian"` (PowerShell `[xml]` one-liner in the source counts tagged vs total per file).
- **Source:** memory/feedback_equipmenttype_civilian_required.md + equipment-armory-system.md (commits 7807b28, 5ae0a2a)

### Register a new notable NPC in BOTH npcs_{culture}.xml AND the culture's `<notable_templates>`
A TAOM culture's notable pool lives in two coordinated files, and the engine reads ONE as the source of truth. **(1)** `Main/_Module/ModuleData/characters/npcs_{culture}.xml` defines `<NPCCharacter id="spc_notable_{culture}_{N}" is_template="true" occupation="X" …>` (the character). **(2)** `Main/_Module/ModuleData/taom_spcultures.xml` `<notable_templates>` block lists `<template name="NPCCharacter.<id>" />` lines — this is the SPAWN POOL that `HeroCreator.CreateNotable` picks from. An NPC defined in (1) but not registered in (2) is unreachable: the engine reuses an existing template (producing clone notables with identical names/traits) or stalls the spawn, because it does NOT enumerate `npcs_*.xml` to build the pool. Same rule applies to Preachers and Headmen.
- **Why missed:** 2026-05-31 cultural-feats 3-pack — added `spc_notable_{isengard,mordor,dolguldur,gundabad}_23` RuralNotable NPCs for the new village-notable-count feats (target ceils 2 → 3) but did NOT register them in `<notable_templates>`; they were unreachable and the engine would have reused `_21`/`_22` — exactly the clone-notable bug they were meant to prevent. Deep-review Agent 5 (Data Flow) caught it. `tools/validate_moduledata.py` flags the reverse (template ref → missing NPC) but NOT an unreferenced NPC.
- **Prevent:** In the same PR, add `<template name="NPCCharacter.spc_notable_{culture}_<N>" />` (ordered by NPC id). Verify with `grep -c '<template name="NPCCharacter.spc_notable_{culture}_' taom_spcultures.xml` == intended reachable count. (Instance of the *partial replication of a multi-layer convention* family.)
- **Source:** memory/feedback_notable_template_two_layer_registration.md + docs/reviews/rca-cultural-feats-3pack-2026-05-31.md + .claude/rules/xml-data.md

### XSLT transforms must pass through all vanilla attributes
XSLT transforms must copy ALL vanilla attributes and elements, then override only what changes. Critical attributes like `is_main_culture`, `can_have_settlement`, `faction_banner_key` are silently dropped if not passed through, causing hard-to-diagnose runtime issues.
- **Prevent:** Always include `<xsl:apply-templates select="@*"/>` and `<xsl:apply-templates select="*[not(...)]"/>` in XSLT templates. Only exclude elements you are explicitly replacing.
- **Source:** memory/feedback_xslt_passthrough.md

### Diff against the INSTALLED vanilla version before mirroring/relying on its data
Before authoring or trusting anything in TAOM that mirrors, extends, or transforms vanilla Bannerlord (scenes, item/culture/clan/kingdom IDs, XML schemas, API signatures), diff it against the CURRENTLY INSTALLED vanilla version — not memory, not the older version TAOM was built against. "It worked before" is not evidence: vanilla renames/removes/re-schemas every patch, and a stale TAOM ref valid in v1.3.15 silently breaks in v1.4.5 and crashes when exercised (often far from the stale value).
- **Prevent (by data type):** Scenes → `tools/audit_scene_names.py` + `audit_battle_scenes.py` vs on-disk `SceneObj/`. TaleWorlds API → `pwsh tools/taom-src.ps1 path <Type>` against installed DLLs. Culture/clan/kingdom/item IDs → grep vanilla `SandBoxCore` XML. XSLT passthrough → diff the vanilla source the transform targets. Always run the scene audits after a version bump. Enforcement artifact: scoped rule `.claude/rules/vanilla-data-comparison.md` (auto-loads on settlements/sp_battle_scenes/spcultures/spclans/spkingdoms/xslt edits).
- **Source:** memory/feedback_compare_against_vanilla_before_mirroring.md + docs/migration/v1.4.x-changes.md + docs/reference/scene-reference-audit.md (Mike standing preference, 2026-05-28)

### Stale settlement scene_name refs crash after a version bump — audit vs on-disk SceneObj
Symptom: "fighting battles near specific places crashes" / entering certain towns/villages crashes after a Bannerlord version upgrade. Root cause: TaleWorlds renames/removes scenes between versions; a `<Location scene_name="X">` in `settlements.xml` pointing at a scene with no matching `SceneObj/X/` folder crashes when the engine loads it (entering the settlement, its houses, or a battle/siege there). This is a stale data reference, NOT an engine-internals bug. Confirmed v1.3.15 → v1.4.5 (2026-05-28): v1.4.5 renamed house-interior scenes (e.g. `battania_house_a_interior_house` → `battania_town_house_b_interior_b_house`, `khuzait_house_a_interior_house` → `khuzait_house_c_interior_a_house`, `sturgia_house_a_interior_house` → `sturgia_town_house_d1_interior_b_house`, `vlandia_house_d_interior_house` → `vlandia_city_house_a_interior_house`) — only the `house_N` refs were stale (taverns were already updated); 61 occurrences across town_L1/M1/RU1/RU2/E1/E2/V4. TAOM_Map carries explicit per-location scene_names for everything, so it's far more exposed than vanilla settlements.xml (~6 explicit scene_names in v1.4.5 SandBox; most resolved by convention).
- **Prevent:** `python tools/audit_scene_names.py` (case-insensitive matching — Windows resolves `HART_ISENGARD` vs `HART_isengard`; an exact-case audit false-positives; also scans `SceneEditData/` to distinguish missing-everywhere from WIP-not-yet-exported). Fix with `python tools/remap_stale_scene_names.py --apply --backup` (verifies each replacement exists on disk). For a missing CUSTOM scene, check `TAOM_Map/SceneObj/` for a near-match (typo like `lotraom_e_osgiliath` vs on-disk `lotrtaom_e_osgiliath` → repoint, don't downgrade to vanilla); fall back to a vanilla scene of the matching settlement type only when no custom exists.
- **Source:** memory/feedback_scene_name_refs_break_on_version_bump.md + docs/reference/scene-reference-audit.md

### XSLT passthrough silently inherits vanilla attributes you didn't intend to keep
When TAOM XSLT overrides a vanilla element (e.g. `<xsl:template match="Culture[@id='sturgia']">`), `<xsl:apply-templates select="@*"/>` preserves EVERY vanilla attribute except the ones explicitly overridden — and vanilla XML may bind attributes you don't realize exist, so your "passthrough" silently inherits them and your new TAOM templates become dead code.
- **Why missed:** Codex Review #227 (Dale culture, 2026-05-26, P1) — Dale's XSLT set 9 military attributes but missed 6 others (`militia_party_template`, `rebels_party_template`, `vassal_reward_party_template`, `settlement_patrol_template_level_1/2/3`); vanilla `spcultures.xml` declares all 6 with sturgia-prefixed values, so passthrough preserved them and Dale's new `militia_dale_template` etc. became dead code. 5 prior `/deep-review` agents missed it because they checked "what we DID is correct" not "what we DIDN'T set, was that intentional." Repeat-offender risk: Rohan (vlandia) has the same gap — `militia_rohan_template` declared but not bound.
- **Prevent:** Before authoring an XSLT culture override, decompile the engine deserializer (`taom-src path TaleWorlds.CampaignSystem.CultureObject`) and enumerate every attribute it reads; read the vanilla XML; for each engine-readable attribute explicitly classify BIND / PASSTHROUGH / N/A and code-comment the decision. Symptom in the wild: a new template/roster/NPC declared but grep finds zero consumers → likely dead because the XSLT binding is missing. Same pattern for heroes.xslt, spclans.xslt, spkingdoms.xslt, spnpccharacters.xslt.
- **THIRD INSTANCE 2026-08-04 (Khand/battania, #374) — the trigger was too narrow.** This entry said "before *authoring* an XSLT culture override". The Khand work was framed as "point Khand's troops at the Rhun roster" — an *edit* to an existing template — so neither this rule nor `/new-culture` was consulted, and the same bug landed a third time (Dale → Rohan → Khand). It bound 6 attributes and let the passthrough inherit **7 more engine-read ones** on Calradian values: `elite_basic_troop`, `villager_party_template`, `militia_party_template`, `rebels_party_template`, `settlement_patrol_template_level_1/2/3`. **The BIND / PASSTHROUGH / N-A enumeration is required for ANY edit to a `Culture[@id=...]` template, including a one-attribute re-point.**
- **Mechanical form (stop relying on judgement):** transform with lxml and diff the emitted element against a sibling already re-themed the same way — `ET.XSLT(...)(vanilla)`, then flag every attribute whose value still matches the vanilla culture id. That is a 10-line script and it found all 7 in one pass. Also check the deserializer before binding: `caravan_party_template` / `elite_caravan_party_template` look bindable but `CultureObject.Deserialize` (v1.4.7) reads only the plural child elements, so binding them is dead markup.
- **FOURTH INSTANCE 2026-08-12 (settlement patrols), and the mechanization now exists, so there should be no fifth.** The third instance recorded the Khuzait/Rhun party templates resolving to Calradian troops as "a pre-existing TAOM gap ... separate content work" and moved on. It stayed separate for eight days, during which every town owned by Dunland, Harad, Rohan, Rhun, Khand, Umbar, Shaghana or Abanissa spawned Calradian patrols, villagers, militia, rebels and caravans. Four of the six retagged cultures never named the patrol attributes at all, so nothing in the file looked wrong. Two things this instance adds that the earlier three did not have:
  - **The check is now a test, not a discipline.** `TAOM.Tests/Core/CulturePartyTemplateTests.cs` runs `spcultures.xslt` over a synthetic vanilla document whose every party-template binding is a unique `PartyTemplate.SENTINEL_*` value, then fails on three states: attribute absent, sentinel survived (unbound, so the passthrough would hand it Calradia), or bound to an id `taom_partyTemplates.xml` does not define. The sentinel half catches ABSENT bindings, the whitelist half catches PRESENT-but-Calradian ones, and battania is the case proving you need both: it named every attribute and was still wrong.
  - **Whitelist, never a blacklist of vanilla prefixes.** Index every id TAOM authors and require membership. TAOM redefines zero vanilla party-template ids, so "not TAOM-authored" is exactly "resolves to Calradian troops". A prefix blacklist needs an exemption for every intentional cross-culture share (Lothlórien uses Rivendell's, Umbar uses Harad's, Khand uses Rhun's) and goes stale on the next engine bump.
  - **Child elements have their own version of the trap, with a worse failure.** `caravan_party_templates` is read only as a child, and `CultureObject.Deserialize` (v1.4.8, `CultureObject.cs:485-497`) does `mBList10.Add(...)` inside a loop over EVERY matching child. It unions, it does not overwrite. So emitting a TAOM caravan block without also adding the wrapper to that block's `not(self::...)` passthrough filter leaves the culture holding both and rolling Calradian roughly half the time, which is worse than a clean miss because it is nondeterministic. The emit and the filter edit must land in the same commit.
  - **Deferring a known gap is the mechanism, not an accident.** The third instance wrote the gap down accurately, in the right file, next to the code, and it still shipped for eight more days. Writing a gap into a comment is not a plan to fix it. Either fix it or file it as an issue with a number the comment can name.
- **Source:** memory/feedback_xslt_passthrough_unintended_inheritance.md + feedback_enumerate_from_source_of_truth.md + .claude/rules/troops.md; third instance `docs/reviews/rca-landless-culture-spawn-2026-08-04.md`; fourth instance `docs/features/culture-playability-wiring.md` "Party-template binding contract"

### A grep over ModuleData that becomes a factual claim must be comment-aware
XML comment spans are invisible to line-oriented greps. `SandBox/ModuleData/spclans.xml` contains three commented-out `<Faction>` blocks (`guardians`, `chosen_of_the_sky`, `freemen`); a raw-text regex finds 98 factions, an `ElementTree` parse finds 95 live ones. The commented three are the ONLY ones lacking `initial_home_settlement`, so a comment-blind grep produces the exact false claim "vanilla ships three clans without a home settlement" — which then propagated into a patch comment, a service comment, a feature doc, a patch-registry entry and an RCA before anyone parsed the file.
- **Why missed:** the three commented entries are single-line while live factions are multi-line, so `grep -c '<Faction id='` returns a plausible-looking small number and reads as a discovery rather than an artifact.
- **Prevent:** any grep over ModuleData whose result becomes a factual claim in a comment, doc, validator or commit message must be re-run with comment spans stripped, or via a parser. `tools/taom_schema.py:_read_stripped` is the reference implementation and exists for exactly this; one-off scripts and investigative greps are where it keeps getting skipped.
- **Source:** `docs/reviews/rca-landless-culture-spawn-2026-08-04.md` (deep-review L5, 2026-08-04)

### After renaming any entity, grep ALL of ModuleData for the OLD name
When renaming a TAOM entity (NPC, settlement, item, faction, kingdom), grepping only the defining file + structurally-linked files (heroes.xslt, lords.xslt, `Languages/**/std_*.xml`) is NOT enough. Human-authored lore/flavor text mentions characters by literal name and lives in `settlements.xml` `text="..."` blurbs, `taom_*_strings.xml`, quest/dialog/`cs_*.xml` narratives, etc., and goes stale silently (and player-visibly).
- **Why missed:** RCA 2026-05-26 — a Gondor lord review renamed `lord_1_45_1` "Vanyalos" → "Berethiel" in `lords.xml`; the data-flow audit traced lords.xml/heroes.xslt/lords.xslt/taom_xslt_strings.xml/Languages (clean) but `settlements.xml:2904` (town_EW7 Bar Melui / Arnach, Forlong's seat) had flavor text *"…through his marriage to Lady Vanyalos"* — player-visible in the Encyclopedia after shipping.
- **Prevent:** Run `grep -rn "OLD_NAME" Main/_Module/ModuleData/` (NO glob filter) and audit every hit. In `/deep-review`, Agent 4 (Completeness) should always include a final loc-consistency grep across the FULL ModuleData tree for any renames in the changeset.
- **Source:** memory/feedback_rename_grep_all_moduledata.md

### Grep-counting XML elements: a `<TagNames>` wrapper overcounts `<TagName>` by 1
`grep -c '<TagName'` also matches a wrapping parent `<TagNames>` (plural), so the count is high by 1. TAOM's `troops_<culture>.xml` wrap N `<NPCCharacter>` elements inside one `<NPCCharacters>`, so `grep -c '<NPCCharacter'` returns N+1.
- **Why missed:** 2026-05-23 (Issue #212 deep-review) — a Completeness agent's count was dismissed as a false positive using `grep -c '<NPCCharacter'`; the agent was right and the CHANGELOG was off by +1 per culture. Self-RCA at `docs/reviews/rca-troop-revamp-212-2026-05-23.md`.
- **Prevent:** Count with a discriminating pattern (`grep -cE '<NPCCharacter\s+id='`) or, better, Python `re.findall(r'<NPCCharacter\s+id="([^"]+)"', content)`. When checking a Completeness agent's count, use a STRICTER method than the agent did, not a sloppier one — cheap-grep validates ">0" but not "=N."
- **Source:** memory/feedback_xml_grep_wrapper_offset.md + docs/reviews/rca-troop-revamp-212-2026-05-23.md

### Match XML element starts with `<Tag\b`, never the bare substring `"<Tag"` — a longer sibling hijacks the match
The sibling lesson above is an off-by-one you can see. This is the same defect where you *can't*: a scanner that splits a file into blocks on `"<skin" in line` also fires on `<skin_color_gradient_points>`. In `skins.xml` that is 3710 matching lines against 140 real `<skin>` elements — a 26× overcount — and a false block-START silently RESETS per-block parser state mid-block, so the tool attributes findings to the wrong block and still prints success. Same trap one level down: `"<eyebrow_mesh" in s` matches the `<eyebrow_meshes>` parent, so an indent derived from "the first child" comes from the parent instead.
- **Why missed:** 2026-07-23 female-elf `skins.xml` swap. The post-condition check walked the file counting `<skin` starts to attribute each `<face_textures>` block to a maturity; `<skin_color_gradient_points>` sits *before* `<face_textures>` in every block, so gender state was wiped exactly where the real defect lived, and the check printed "zero elf refs remain" over four broken `m_elf_basemesh_a1_head` references. A separate instance in the same session mis-indented 17 `<eyebrow_mesh>` lines. **Most telling: during `/deep-review` of this very change, 2 of 5 agents grep-counted `<tattoo_material` (which matches the `<tattoo_materials>` parent) and both reported the count one high — one of them quoting the `<TagNames>` lesson above two paragraphs earlier.**
- **Prevent:** word-boundary the element start — `re.match(r'<skin\b', line)` or `grep -nE '<skin($|[[:space:]]|>|/)'` — or drop the hand-rolled splitter for `xml.etree.ElementTree` and iterate real elements. Before trusting any block splitter, print the block COUNT and assert it equals what a parser reports. A failure mode this durable needs a mechanical guard, not another paragraph of documentation.
- **Source:** docs/reviews/rca-elf-female-skins-2026-07-23.md

### Smoke-test every new/modified ModuleData XML with an actual parser before commit
Before committing any modified/new XML under `Main/_Module/ModuleData/` (or a sibling TAOM module's `ModuleData/`), run an XML-parser smoke test (`pwsh -Command '[xml]$x = Get-Content -Raw "<file>"; "OK"'`). XML spec edge cases that pass eyeball review but reject at parse time include `--` inside a `<!-- ... -->` body (FORBIDDEN), unescaped `&`/`<`/`>` in attribute values, mismatched tags, duplicate attributes, invalid attribute names, stray BOM, and encoding mismatches. The engine's XML loader is MORE permissive than `[xml]` AND less verbose — by the time it surfaces an error you're staring at a black-screen crash log with no file:line.
- **Why missed:** Bandit Management 2026-05-27 — `taom_partyTemplates.xml` had a comment block using `--` as a separator (`{culture}_raider_party_template  -- regular bandit warbands…`); the `[xml]` parser rejected it, but it shipped past 5 `/deep-review` agents because all read it semantically — none parsed the modified XML (Agent 1 is C#-focused; Agent 4 checks SubModule.xml registration; Agent 5 traces references). Codex caught it as CRITICAL. Fix: replaced `--` with `=`.
- **Prevent:** Add a parser smoke-test to Agent 5 (Data Flow): every modified `ModuleData/` XML must parse via `[xml]$x = Get-Content` without throwing → CRITICAL if not. Anti-pattern: assuming "the engine will tell us if the XML is bad" — it often won't.
- **Source:** memory/feedback_xml_parser_smoke_test_before_commit.md + .claude/rules/xml-data.md

### Bannerlord prefix-ref matchers must be attribute-agnostic, not anchored on `id=`
When building a matcher for prefixed references (`Item.`, `NPCCharacter.`, `Culture.`, `PartyTemplate.`) for validation/auditing, match the PREFIX on ANY attribute (`="Prefix\.(...)"`), never anchor on a specific attribute name like `id=`. The prefix is globally unambiguous but the carrying attribute varies: items `id="Item.x"`, troop-in-stack `troop="NPCCharacter.x"` (NOT `id=`), upgrade target `id="NPCCharacter.x"`, culture `culture="Culture.x"`, party templates `villager_party_template="PartyTemplate.x"`, etc. Definitions never carry the prefix in their value (`<NPCCharacter id="x">`), so a prefix-anchored matcher never mistakes a def for a ref.
- **Why missed:** `tools/taom_schema.py` shipped (pre-review) with ref patterns anchored on `id=` — dead-troop refs inside `<PartyTemplateStack troop="NPCCharacter.x"/>` passed silently, a false negative in a tool whose whole job is catching false negatives. Deep-review 2026-05-30 caught it HIGH. Root cause: the pattern was derived from ONE sample file (`troops_gondor.xml`, where troop refs only appear as `upgrade_target id=`). Generalising to `="NPCCharacter\.(...)"` raised troop-ref coverage 628 → 2,869 with zero new false positives.
- **Prevent:** Before settling a prefix-ref matcher, `grep -rho '="<Prefix>\.[^"]*"' Main/_Module/ModuleData` to enumerate every attribute the prefix appears on; write the regex attribute-agnostic; add a test exercising a NON-`id=` attribute (e.g. `troop=`).
- **Source:** memory/feedback_prefix_ref_matchers_are_attribute_agnostic.md + docs/reviews/rca-moduledata-validation-2026-05-30.md

### A validator's SCOPE is part of its correctness — a clean PASS only ever means "clean within the scope you pointed it at"
`tools/validate_mesh_refs.py` existed since #262 specifically to test "a missing `bo_` collision body causes infinite battle-load hangs", and it catches the bug at the exact line — but its `DEFAULT_ITEMS` scanned `ModuleData/LOTRLOME_items/`, and crafting pieces live in `ModuleData/LOTRLOME_crafting_pieces.xml`, one directory up. **The tool built to catch this class never looked at the file containing it, and reported PASS the whole time.** When a purpose-built validator says clean and the bug is still live, suspect its scope before you suspect its logic — and never let a clean run print "hypothesis WEAKENED" when it can only support "clean within scope" (that text has been corrected).
- **Why missed:** LOTRLOME_Armory v2.0.8 shipped two `body_name` typos (surfaced 2026-07-16, #352). `bo_dunland_caerdh_sword_blade_2h` (should be `..._2h_a` — the `mesh` attr carried the `_a`, the `body_name` didn't) hung **every siege with Dunland troops**: `PreloadHelper.WaitForMeshesToBeLoaded` polls every registered body name and only exits once each resolves, so one bad name spins the main thread forever — no crash, no error log, one core at 100%. `bo_wm_harad_spear_a02_blade` (should be `..._a02_head`; Harad spears use `_head`, only swords/glaives use `_blade`) went unreported longer because it reaches missions via a crafting-template `UsablePiece` rather than a troop roster, so only players who crafted it hung. Both assets shipped correctly — only the refs were wrong.
- **Prevent:** Run `python tools/validate_mesh_refs.py --scan-bodies` after any weapon/armor/crafting authoring; the default scope now covers `ModuleData/`. Three traps beyond running it. (1) **A missing asset is a typo until proven otherwise** — the reporting user concluded "never shipped" and worked around it by replacing the sword in their loadout; the body sat in `pack1.tpac` one suffix away. Diff the ref against the packaged names for a near-match before deleting content. (2) **Verify a tool's assumptions, not just its output** — Tier C byte-scanned raw bytes because its header asserted bodies "are NOT in the .tpac TOC"; they are (`PhysicsShape`, 382 in pack1), so the tier was coarse for no reason for a year. (3) **A grep that returns nothing is not evidence of absence** — an empty `grep -rln "body_name" tools/` (a silent false negative) is what convinced this session no body validator existed, sending it to extend the wrong, superseded tool before the overlap was caught. Confirm a negative grep with a positive control.
- **Source:** #352 (user field report; scope fix + exact `PhysicsShape` Tier C in `tools/validate_mesh_refs.py`)

### A crafting-piece head must exclude the attack it has no damage for — and the weapon DESCRIPTION decides that, never the piece's name
`excluded_item_usage_features` tokens are **name fragments of an `item_usage_sets.xml` id**, not capability flags: `GetItemUsage()` (`Crafting.cs:423-447`) strips every token any used piece excludes and joins the survivors with `_`. So a swing-only head (`<Swing>`, no `<Thrust>`) must exclude `thrust` **iff its description carries a `thrust` token** — mandatory for `Mace` (`onehanded:block:shield:tipdraw:swing:thrust`) and all four sword descriptions, unnecessary for `OneHandedAxe`/`TwoHandedAxe` (`onehanded:shield:axe` / `twohanded:widegrip:axe` have no such token). The mirror applies to thrust-only heads and `swing`. Never declare damage you then exclude: vanilla ships **zero** blades with a `<Thrust>` element plus `excluded_item_usage_features="thrust"`, because the item card would advertise a stat the animation set cannot deliver. And when auditing, enumerate **cross-slot combinations**, not pieces — exclusions are unioned across every piece in the weapon, so a Blade+Handle pair can compose a name no single piece would.
- **Why missed:** TAOM shipped all 20 blade pieces of the `Mace` description without the `thrust` exclusion (vanilla tags 30/30 of its own mace heads), giving 19 `<CraftedItem>`s across 7 cultures a thrust attack with `ThrustDamageType = DamageTypes.Invalid` and factor 0 (`BladeData.cs:39`). There is no crash, assert, log line, or validator for it — the engine accepts any composed name and `WeaponComponentData` just stores the string. It surfaced only because a session asked what the attribute does. The near cause: `weapon-creation-workflow.md` documented one of the five shipped values (`swing`), and its **correct** swing-only *axe* example teaches the omission that is wrong for maces — several of the 20 heads are even named "Orc Axe" while authored into `Mace`.
- **Prevent:** Read [`docs/reference/item-usage-features.md`](../../reference/item-usage-features.md) before adding a blade head — it carries the token table, the per-family vanilla convention, and the union-audit method. Do **not** strip inert exclusions: vanilla ships 17 fully-inert ones of its own 93 (`mace_head_31`–`39` tag `thrust` while appearing only in `TwoHandedMace`), so tagging a head by its own nature is deliberate practice. Candidate mechanisation (not built): a `validate_moduledata.py` check recomputing every reachable piece × description name and flagging unknown names, declared-but-excluded damage, and missing exclusions.
- **Source:** [rca-crafting-usage-features-2026-07-26.md](../rca-crafting-usage-features-2026-07-26.md)

### A `HorseHarness` without `<Armor family_type>` is silently unequippable on every mount
`ArmorComponent.Deserialize` defaults a missing `family_type` to **0 — the human family** — and the v1.4.7 inventory screen compares it against the equipped mount's `Monster.FamilyType`, returning false with **no message, no tooltip, no log line** (`SPInventoryVM.IsItemEquipmentPossible`, `:4112`); the same comparison at `:3923` force-unequips a harness that an equipment-set XML placed directly (XML rosters bypass the VM entirely, so the item looks equipped until the player's first inventory transfer). `Equipment.IsItemFitsToSlot` checks item *type* only, so nothing upstream rejects it either. Mount-side family type comes from the monsters XML alone — `HorseComponent.Deserialize` never reads `family_type` off the `<Horse>` element, so those attributes in `LOTRAOM_horses.xml` are dead data and a comment crediting them (line ~524) is wrong.
- **Why missed:** `starter_cavalry_gondor_horse_armor_a` ("[Gondor] Riding Caparison") was hand-authored 2026-05-21 by copying stats — not the full attribute set — from an existing harness, shipped with `Not-tested:`, and stayed broken until a player bought one on 2026-07-29 (#364). The failure is invisible by construction: no error, no crash, and the career start *looks* correct in the character-creation preview. It was the only one of 86 harness ids missing the attribute, and also the only one missing `<Flags Civilian="true"/>` — a second silent gate (`:4042`, civilian mode only).
- **Prevent:** `python tools/validate_moduledata.py` now emits `MISSING_HARNESS_FAMILY_TYPE` (harness with no `family_type`) and `HARNESS_FAMILY_MISMATCH` (a `Horse` + `HorseHarness` pair in one `EquipmentSet`/troop `EquipmentRoster` whose family types disagree) as ERRORs; the pre-commit gate blocks on them. When authoring any harness, copy a *complete* sibling block that uses the same mesh rather than transcribing stats — the mesh sibling also carries the right `mane_cover_type` and Civilian flag. Family types: 1 horse/warg/spider, 4 chariot, 10 elephant/mûmakil.
- **Source:** #364 + [armory-guide.md](../../reference/armory-guide.md) "Harness rule" + [moduledata-validation.md](../../features/moduledata-validation.md)

### An unconditional `<xsl:template match="X"/>` deletes a whole earlier contribution — model the strip or you reason about a world the game never builds

`<game>/Modules/TAOM_Map/ModuleData/settlements.xslt` contains `<xsl:template match="Settlement"/>` —
one empty template, no test, which DELETES every vanilla `<Settlement>` (494 of them). TAOM_Map's own
988 replace them wholesale. Any tool reasoning about merged ModuleData must therefore walk the
contributing modules in load order (`Native`, `SandBoxCore`, `SandBox`, `CustomBattle`, `TAOM_Map`)
and RESET the accumulated set when a module strips everything before it. `build_settled_cultures`
without that reset counts vanilla's 494 deleted settlements, reports every culture as landed, and
prints PASS while the game crashes on a landless culture (`HeroSpawnCampaignBehavior.SpawnLordParty`
— see [data-content-cultures.md](data-content-cultures.md)).
- **Why missed:** an empty template is a single self-closing line that reads as a no-op, and the
  deletion leaves no marker anywhere in the merged result — the "before" state is simply absent. Every
  other XSLT lesson in this file is about a transform that changes an element; this one is about a
  transform that makes 494 of them stop existing, which no amount of reading the OUTPUT will tell you
  about.
- **Prevent:** before writing any tool over merged ModuleData, grep the module's XSLT for empty-body
  templates (`<xsl:template match="[^"]*"\s*/>`) and model each one explicitly in the merge. State the
  merge semantics in the builder — append, replace, or strip-then-append — rather than assuming the
  additive case.
- **Source:** #374 (`tools/taom_schema.py` `build_settled_cultures`)

### The attribute-exclusion trap: `@*[local-name() != 'x']` drops an inherited attribute the template may or may not restore

`spclans.xslt` and `spkingdoms.xslt` copy attributes with `@*[local-name() != 'initial_home_settlement']`
— dropping the inherited attribute — and then re-add it further down with `<xsl:attribute>`. Reading
the vanilla XML alone therefore answers a question about the wrong document, and it is wrong in BOTH
directions: a first pass that modelled the vanilla attribute reported 23 dangling
`initial_home_settlement` refs including the whole `clan_battania_*` set; a second pass that ignored
the exclusion filter reported zero. Neither number was real. This is the mirror of the passthrough
lesson above — passthrough silently KEEPS what you didn't intend, exclusion silently DROPS what you
assumed was kept.
- **Why missed:** the exclusion is one predicate inside an `@*` select that reads as a passthrough at
  a glance, and the `<xsl:attribute>` that restores it sits far enough down the template body that
  neither half is visible from the other. Both wrong answers were internally consistent, so nothing
  in either analysis contradicted itself.
- **Prevent:** run the transform and read the attribute off the OUTPUT — the Variag culture check did
  exactly this (lxml against vanilla `spcultures.xml`) and is the only step that produced a number
  worth quoting. Before trusting any inherited attribute, grep the template for `local-name() !=` and
  for a matching `<xsl:attribute name="…">`; presence of one without the other changes the answer.
- **Source:** #374 investigation, 2026-08-04

### A regex of `<tag ` misses `<tag\n  attr=…` — this project's XML wraps attributes onto continuation lines

Counting the actions inside `as_dwarf_warrior` in the live
`<game>\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml` with `<action ` returns **134**. The same
slice matched with `<action[\s>]` returns **4,842**. The generated blocks put the element name on one
line and its attributes on the next, so a pattern anchored on a trailing space only ever sees the
handful of single-line entries — a 36× undercount that reads as a plausible figure rather than as a
broken query. It was one step away from going into an issue comment as evidence that the dwarf action
set had never been populated.
- **Why missed:** `<action ` is the obvious pattern and it is correct on every hand-authored XML in
  the repo. The failure surfaces only in machine-generated or reformatted files, and a partial match
  count is indistinguishable from a real one.
- **Prevent:** match `<tag[\s>]`, or parse the file. `tools/check_prefab_budget.py` already uses
  `rb"<game_entity[\s>]"` for exactly this reason — copy that shape instead of re-deriving it. Before
  any element count goes into a doc, an issue, or a CHANGELOG, reproduce it by a second method (lxml
  `findall`, or a count of the closing tag) and confirm the two agree.
- **Source:** #300, `docs/audits/issue-triage-2026-08-08.md`

### `grep -r` over a live ModuleData folder reads `.bak` files the engine never loads

The installed `LOTRLOME_Armory/ModuleData/` holds **40** backup files sitting beside the live ones —
`action_sets.xml.bak`, `action_sets.xml.bak_actionfix_20260803`, four `skins.xml.bak-*`,
`monsters.xml.bak-sauron`, more under `Animations/` and `LOTRLOME_items/`. A recursive grep reads all
of them and reports values from an arbitrary past snapshot as the current state of the data. Worked
example: `grep -rl "Culture\.rohan"` over that folder returns two hits
(`LOTRAOM_horses.xml.bak-dangling-culture-20260802` and `…bak-startergear-20260630`); the same grep
with `--include='*.xml'` returns **zero**, which is the true live answer. Reading the first result
would have reopened a culture-alias regression that was fixed on 2026-08-02.
- **Why missed:** this is the flip side of the backup-extension rule elsewhere in this file. Those
  files are deliberately named so the engine's `*.xml` glob cannot see them — which is precisely what
  removes them from your mental model when a grep hands back a real path with a real value. Nothing
  signals that the directory being searched is now largely history.
- **Prevent:** search the exact file the engine loads, not the directory. Resolve it through the
  module's `SubModule.xml` `<XmlName id="…" path="…"/>` first, then grep that one path. Where a
  directory sweep is genuinely wanted, `--include='*.xml'` filters the current `.bak-*` naming, but
  that is a convention rather than a guarantee — read the matched paths before drawing a conclusion
  from a count.
- **Source:** `docs/audits/issue-triage-2026-08-08.md`, 2026-08-08

---

### ModuleData XML breaks attributes onto their own lines, so `grep 'elem attr='` silently under-reports

Hunting the race-to-voice bindings in `LOTRLOME_Armory/ModuleData/skins.xml`,
`grep 'voice_type name='` returned 78 hits, every one of them a vanilla `male_0x` / `female_0x`.
The conclusion drawn was that TAOM's three custom voice definitions bind to nothing and are dead
data, and that conclusion went to the user. It was wrong. The file holds 45 custom refs, written as:

```xml
<voice_types>
    <voice_type
        name="dwarf_01" />
</voice_types>
```

Vanilla's entries are single-line, the mod's hand-authored ones are not, so the pattern matched
exactly the subset that made the wrong answer look complete and consistent. A multiline-aware
`<voice_type\s+name="…"` recovered all 45 and produced the real picture: 7 races bound, 3 of them
diluted with vanilla entries alongside the custom one.

- **Why missed:** the grep returned a large, plausible, internally consistent result set. Nothing
  about 78 vanilla hits reads as truncation, and the shape of the answer (all vanilla, no custom)
  was exactly the shape the hypothesis predicted. Confirmation arrived in the form of missing
  evidence, which is the form hardest to notice. The same grep run against the repo's snapshot copy
  "corroborated" it, because both files share the formatting.
- **Prevent:** for any ModuleData element-plus-attribute search, match across whitespace
  (`<elem\s+attr="`) rather than assuming a single line, and prefer a parser over a grep when the
  answer is a count or a mapping rather than a location. Cross-check any zero-custom-entries result
  against a positive control: grep the element name alone and compare counts. Here,
  `grep -c '<voice_types'` returned 141 blocks against 78 attribute hits, and that mismatch was
  visible in the same output that produced the wrong conclusion.
- **Corollary that generalises past XML:** a user contradicting a tool-derived finding ("I can
  confirm those work in game") outranks the tool. Field observation beat static analysis here, and
  the productive response was to hunt the mechanism rather than to re-assert the grep. Treat the
  contradiction as a pointer to a measurement error, not to a disagreement.
- **Source:** voice-system research, 2026-08-12. Full state table:
  [`docs/features/kingdom-voices.md`](../../features/kingdom-voices.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/LESSONS-LEARNED.md](../LESSONS-LEARNED.md)

<!-- backlinks-end -->
