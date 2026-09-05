# RCA — ModuleData Validation Tooling (deep-review, 2026-05-30)

**Feature:** `tools/validate_moduledata.py` + `tools/taom_schema.py` + `tools/schemas/*.json` — a schema-driven cross-reference + validation engine for TAOM ModuleData XML.

**Top line:** `/deep-review` ran 4 adapted agents (Python-correctness, tooling-correctness, completeness, schema→engine data-flow; the C#-API agent was N/A — zero TaleWorlds usage). 2 HIGH + 5 MED/LOW findings confirmed and **all fixed in-session**. The two HIGH findings are the important ones: both were **false-negative bugs in a tool whose entire job is to prevent false negatives** — it would have silently passed real broken references. Net coverage after the fix: troop-ref checks rose from 628 → 2,869 references (+357%) with zero new false positives.

## Findings

| # | Sev | Bug | Category | Why missed (by the author) | Preventive action |
|---|-----|-----|----------|----------------------------|-------------------|
| 1 | HIGH | Ref patterns anchored on `id=` (`\bid="NPCCharacter\..."`) — missed `troop="NPCCharacter.X"` in `<PartyTemplateStack>`, so dead-troop refs in party templates passed silently | Sample-driven assumption | Built the ref regexes from `troops_gondor.xml`, where troop refs appear only as `upgrade_target id=`. Never grepped the OTHER attributes that carry the prefix (`troop=`). | Prefix-based matchers must key on the **prefix**, not the attribute name. Patterns are now attribute-agnostic (`="NPCCharacter\..."`). Comment in `REF_KINDS` records why. |
| 2 | HIGH | Equipment schema glob `taom_equipment_sets_*.xml` — `taom_lord_template_equipment.xml`, `taom_child_equipment_templates.xml`, `taom_education_equipment_templates.xml` were outside ALL schemas → no dup-id / civilian-type check on them | Partial-enumeration | Built the glob from the filenames visible in a truncated `Glob` listing, not from the full `equipmentsets/` directory contents. | Broadened to `equipmentsets/*.xml`. Schema `applies_to` should glob the directory broadly unless a file must be excluded for cause. |
| 3 | MED | Dead fields: `Schema.id_space`, `Registries.taom_npc_def_files` (cost a full rglob), unused `Schema.description` | Aspirational plumbing | Ported more schema-model richness without wiring every field to a consumer in the same change. | Removed `id_space` + `taom_npc_def_files`; gave `description` a consumer (CLI prints it per schema). Repeat of `feedback_no_aspirational_enum_values`. |
| 4 | MED | A schema declaring an unknown/typo'd `special_rules` string was silently ignored (no handler, no error) | Silent no-op | Hardcoded the dispatch (`if "civilian_equipment_type" in ...`) with no `else`. | `Schema.from_json` now fail-fasts (`ValueError`) on any `special_rules` outside `KNOWN_SPECIAL_RULES`, plus checks required fields are present. |
| 5 | MED | With the game install absent, item/troop/party registries are TAOM-only → every vanilla ref false-positives; tool exited 1 with unusable output | Env-failure path unconsidered | Only exercised the happy path (install present). | When `game_modules is None`, those three registries are emptied → the empty-registry guard skips the dependent checks; the CLI reports the skip. Culture/dup-id/civilian/enum still run. (`environment-failures.md`) |
| 6 | LOW | `_ITEM_DEF_RE` lacked `re.S` while the 3 sibling def-regexes had it (latent: multi-line `<Item>` defs would be missed) | Inconsistency | Copy-paste drift. | Added `re.S`. |
| 7 | MED | 3 emitted codes untested (`BROKEN_PARTY_TEMPLATE_REF`, `DUPLICATE_CULTURE_ID`, `DUPLICATE_ROSTER_ID`); no malformed-XML / no-schema-file / fail-fast tests | Headline-only testing | Wrote tests for the "named" bug classes, not for every code the engine can emit. | Added 9 tests (now 19 total). Rule: test every issue code the engine can emit, positive + at least one negative. |

Findings I **verified as UNFOUNDED** (evidence-over-claims, did not act): `_duplicate_ids` "missing DOTALL" (it has `re.S`); `item_roots` "double-appends moduledata" (appended once); `_entry_by_line` "confused by `<skill id=>`" (the `awaiting` guard prevents it); `_civ` "under-matches `_civilian_`" (`_civ` IS a substring of `_civilian`); `VANILLA_CULTURES` "incomplete" (complete for the code-only cultures it backstops).

## Root-cause pattern: generalising from a partial view

Findings #1 and #2 are the same mistake twice: **generalising a rule from a partial sample instead of enumerating from the full source of truth.** #1 derived ref shapes from one troop file; #2 derived a glob from a truncated file listing. This is a *repeat-offender* pattern in TAOM — it is exactly what these existing memories already warn about:

- `feedback_enumerate_from_source_of_truth.md` — enumerate from the upstream source, not the existing subset.
- `feedback_classify_by_grep_not_by_assumption.md` — grep all the data before classifying.
- `feedback_multi_folder_id_uniqueness.md` / `feedback_verify_troop_ids_against_canonical_xml.md` — verify ids/refs against canonical XML, not naming symmetry.

The irony is pointed: a tool **built to catch reference bugs** shipped (pre-review) with reference-coverage bugs caused by the very enumeration shortcut it exists to defend against. Because the pattern is a repeat offender, the preventive action is stronger than a one-off note — see below.

## Why the standard agent set didn't catch these (and what did)

The 5 core `/deep-review` agents are **C#-centric** (ADR/adapter compliance, TaleWorlds API, GC/hot-path, Harmony categories). On a pure-Python tooling change, agents 1 (C# standards) and 2 (Bannerlord API) are almost entirely N/A. Findings #1, #2, #4, #5 were caught only because the review was **adapted**: the Step-2c "tooling-correctness" agent (regex over/under-match, false-negative analysis) and the cross-system data-flow agent (declared-but-unused fields, unreachable checks) are the agents that fit this changeset. Lesson: when `/deep-review` runs on `tools/**/*.py`, swap the C#-API agent for the tooling-correctness agent and lean on data-flow — which is what was done here.

## Codex adversarial pass (2026-05-30)

After the deep-review fixes, `/review-codex` ran an independent adversarial pass (`gpt-5.5`, `xhigh`). Verdict: **0 CRITICAL / 2 HIGH / 2 MED / 0 LOW**. Codex **correctly disputed all 8 weak suspects** in the prompt (attribute-agnostic patterns, ReDoS, sentinels, entry attribution, empty-registry skip, dup-item scoping) with real-data evidence — zero false positives. All 4 confirmed findings were verified against source and **fixed in-session**:

| # | Sev | Bug | Why the deep-review missed it | Fix |
|---|-----|-----|-------------------------------|-----|
| C1 | HIGH | Culture registry polluted: `_CULTURE_DEF_RE` rglob'd the whole tree, so `<Culture id="empire_w">` in `taom_careers.xml` (career-eligibility groups) + `dale` in `cc_body_properties.xml` + comment placeholders became "valid cultures" → `UNKNOWN_CULTURE` false-negative (a `Culture.dale` typo would pass) | Both the tooling and data-flow agents inspected the culture registry but ASSUMED `<Culture id=>` only appears in culture-definition files. Neither grepped the element across all ModuleData. | Build cultures from authoritative files only (`taom_spcultures.xml` + vanilla `spcultures.xml`) via new `_scan_files`; registry 41→36, all 26 real refs still resolve. |
| C2 | HIGH | NPCCharacter dup-id/enum checks missed 248 defs in `taom_wanderers.xml`, `taom_education_character_templates.xml`, `named_companions/` | I noticed the gap MYSELF during deep-review and *deferred* it (deleted `taom_npc_def_files` instead of using it), under-weighting the risk. The completeness agent flagged test gaps, not file-coverage gaps. | Broadened npccharacter schema `applies_to` to all NPC-def files + a description note mandating future files be added. |
| C3 | MED | Item registry treated comment/config `<Item id=>` as definitions (the `...` placeholder in a `culture_marketplace_config.xml` comment) | No agent checked whether commented/config `<Item id=>` pollutes the registry — same comment-scan blindness as C1. | Strip XML comments in all registry scans (`_read_stripped`). |
| C4 | MED | Civilian rule under-matched: only ids with `_civ`, only the first `<EquipmentSet>`. Real `child_template_*` civilian rosters (114/114 tagged) were uncovered. | The tooling agent reasoned about the rule abstractly ("low risk in practice"); Codex READ `taom_child_equipment_templates.xml` + `taom_lord_template_equipment.xml` and found real civilian rosters without `_civ`. | Detect `child_template`/`_civ` (demonstrably civilian), check ALL EquipmentSets; **exclude** `child_education_*` (0/784 tagged — unknown convention, would be 784 false positives). Documented scope gap. |

**Root-cause reinforcement:** C1, C2, C3 are the *same* "generalising from a partial view" pattern as the two deep-review HIGHs — now **5 findings in one feature**. The matcher side is covered by the new memory below; the registry side adds: **build registries from explicitly-enumerated authoritative definition files, strip comments, and when a schema scopes coverage by glob, grep the whole tree for the entry element to confirm no def-bearing file is uncovered.** C4 reinforces a second lesson: *read the real data files, don't reason about a rule abstractly* — exactly why `/review-codex` (which reads source) caught what the abstract-reasoning agent rated low-risk.

**Process note (3h/3i):** the heavy REVIEW-LOG / AGENTS.md "lessons" machinery is calibrated to the recurring C# feature-review pipeline; this one-off Python-tooling adoption is recorded here in the RCA instead (proportionality). Codex's strong performance — disputing weak suspects with real-data evidence and finding the scan-scope class — is the notable signal.

## Feedback memories to codify

1. **`feedback_prefix_ref_matchers_are_attribute_agnostic`** (new) — when matching Bannerlord prefixed references (`Item.`/`NPCCharacter.`/`Culture.`/`PartyTemplate.`) for validation, match on the prefix across ANY attribute; never anchor on a single attribute name (`id=`). Troop refs appear as `troop=`, party-template refs as `*_party_template=`, etc. Grep all attributes carrying a prefix before settling the matcher. Cross-link: `feedback_enumerate_from_source_of_truth`.
2. Reinforce `feedback_no_aspirational_enum_values` — extend its scope from "enum values" to "any dataclass field / registry entry": every field needs a consumer in the same change (finding #3).

No new always-load rule is warranted; these are tooling-authoring notes, codified as memories.

## Second deep-review pass (2026-05-31 — after the MCP server was added)

A second `/deep-review` ran once the MCP server + query API + hook were added (4 agents adapted for the Python/config/Bash surface; the C#-standards and TaleWorlds-API agents were N/A and skipped). Findings (all confirmed against source + fixed in-session):

| # | Sev | Finding | Fix |
|---|-----|---------|-----|
| D1 | MED | `party_template_exists` existed in the query API but had **no MCP tool** and **no test** — an asymmetry across the parallel surfaces (engine→query→MCP→tests). | Added the `party_template_exists` MCP tool (8→9) + a query test. |
| D2 | MED | `find_references(kind="npccharacter")` silently fell back to an all-prefix search, because the query kind is `"troop"` but the engine's `REF_KINDS` name is `"npccharacter"`. | Added a `_KIND_ALIASES = {"npccharacter": "troop"}` normalization + a test. |
| D3 | LOW | MCP `find_references` didn't expose the `limit` parameter (always capped at 200). | Exposed `limit` on the tool. |
| D4 | LOW | No automated test of the MCP server (manual/in-process only). | Added `tools/tests/test_taom_mcp_server.py` (skips if `mcp` SDK absent; install-independent assertions). |
| D5 | LOW | `_build_query()` startup failure (bad schema / unreadable ModuleData) gave a bare traceback. | Wrapped to print a clear diagnostic to stderr, then re-raise (fail-fast, but informative). |

**Disputed (false positive, not fixed):** an agent rated HIGH that the hook's `Main/_Module/ModuleData/*.xml` `case` glob "misses nested files (`troops/`, `characters/`)". **Disproven empirically** — bash `case` pattern matching treats `*` as matching `/` (unlike filename globbing), so nested paths DO match; the earlier hook test had already blocked a file at `_hooktest/troops_hooktest.xml`. The haiku agent applied the filename-globbing rule to `case` pattern-matching. Recorded as an evidence-over-claims win (verified before acting).

**Root-cause pattern (D1–D2):** *parallel-surface completeness* — when a capability exists in one front-end (the query API), it must be mirrored across **all** the parallel surfaces (MCP tool + test + docs) and the **naming must be consistent** across layers (`troop`/`npccharacter`). This is the same "complete the parallel surface / mirror the sibling's full convention set" lesson as the Codex NPC-file-coverage gap (C2) and the harness-facts "mirror the sibling hook's FULL convention set" rule — generalised here to: *when you add a method/front-end, enumerate every parallel surface (engine, query, MCP, CLI, hook, test, doc) and update or consciously skip each one.* No new memory file needed; this extends the existing C2 lesson.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
