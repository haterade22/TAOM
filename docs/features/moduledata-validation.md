# ModuleData Validation (schema-driven cross-reference validator)

## Overview

A single reusable, read-only validator for TAOM's Bannerlord ModuleData XML. It builds id registries from the installed game + the TAOM repo, resolves prefix-based cross-references (`Item.` / `NPCCharacter.` / `Culture.` / `PartyTemplate.`), and runs per-schema duplicate-id / enum / civilian-type checks, emitting a severity-classified report. It consolidates the scattered one-shot validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, the equipment-`equipmentType` PowerShell snippet, the duplicate-id-across-Armory-folders checks) into one schema-driven engine.

The same engine backs **three consumers**: a **CLI** (`validate_moduledata.py` — batch report), a **pre-commit hook** (`.claude/hooks/check-moduledata-validation.sh` — blocks Claude-driven commits on ERRORs), and an **MCP server** (`taom_mcp_server.py` — interactive agent queries like "does this item exist?" / "what references this troop?").

A schema-driven engine (`SchemaDefinition` / cross-reference / validation services) with an MCP-tool surface, implemented in Python.

## Why This Exists

TAOM repeatedly ships the same data-integrity bug classes, each previously caught (if at all) by a separate ad-hoc script run by hand:

- **Underwear bug** — a troop references `Item.X` that resolves to no item → spawns naked. (`BROKEN_ITEM_REF`)
- **Dead troop ref** — `upgrade_target`/party-template `troop=`/culture `basic_troop=` points at a deleted troop. (`BROKEN_TROOP_REF`)
- **Stale culture / "wrote rohan instead of vlandia"** — `culture="Culture.X"` where X is not a real StringId. (`UNKNOWN_CULTURE`)
- **Duplicate item id across Armory folders** — engine silently shadows one. (`DUPLICATE_ITEM_DEF`)
- **Missing civilian `equipmentType`** — Faramir/Boromir wrong-outfit bug. (`MISSING_CIVILIAN_TYPE`)
- Duplicate NPC/culture/roster ids; invalid `default_group`; broken party-template refs.

"Schemas are the source of truth": field/enum/ref knowledge lives in `tools/schemas/*.json`, not hardcoded in Python.

## Architecture

```
tools/schemas/*.json   (declarative source of truth: entry element, id, enums, special rules)
        │  load_schemas()
        ▼
tools/taom_schema.py   ── build_registries(moduledata, game_modules)
   Registries (items, item_def_files, npccharacters, cultures, party_templates)   ← injected (testable)
   REF_KINDS (prefix-based, attribute-agnostic)
   Validator.run():
     pass 1  global cross-reference sweep  (every *.xml under ModuleData)
     pass 2  per-schema: duplicate-id, enum, civilian-type rule
     pass 3  duplicate item definitions across Armory folders
        │
        ├──▶ tools/validate_moduledata.py                  (CLI: batch report, --json, --code, exit 1 on ERROR)
        ├──▶ .claude/hooks/check-moduledata-validation.sh  (PreToolUse gate: blocks commits on ERROR)
        └──▶ tools/taom_query.py ──▶ tools/taom_mcp_server.py  (MCP: interactive per-id queries for agents)
```

The CLI, hook, and MCP server are thin front-ends over the same engine — the engine is the single source of validation logic; each front-end only changes *how/when* it is invoked (batch, commit-gate, interactive).

**Key design decisions:**
- **Registries are injected**, so the engine is unit-testable with synthetic data (no game install needed).
- **Cross-ref patterns are attribute-agnostic** — they match the prefix on ANY attribute (`id="Item.x"`, `troop="NPCCharacter.x"`, `culture="Culture.x"`, `*_party_template="PartyTemplate.x"`). Anchoring on `id=` silently misses party-template `troop=` refs (deep-review 2026-05-30, HIGH). Definitions never carry the prefix in their value, so a def is never mistaken for a ref.
- **Empty registry → skip that ref kind** (a registry that couldn't be built is "unavailable", not "everything is broken"). With no game install, item/troop/party-template checks are skipped (the CLI reports the skip); culture/dup-id/civilian/enum still run.
- **Fail-fast schema load** — an unknown `special_rules` string or a missing required field raises at load, never a silent no-op.

## Configuration

`tools/schemas/*.json` — one schema per XML type. Fields:

| Key | Meaning |
|---|---|
| `applies_to` | globs (relative to ModuleData) the schema covers |
| `entry_element` | XML element whose `id` is an entry (for dup-id + enum + attribution) |
| `id_attribute` | the id attribute name (default `id`) |
| `duplicate_code` | issue code emitted for a duplicate (`DUPLICATE_NPC_ID`, etc.) |
| `enums` | `{attr: [allowed values]}` → `INVALID_ENUM` (warning) on mismatch |
| `special_rules` | named handlers; currently only `civilian_equipment_type` |
| `description` | shown by the CLI |

Cross-reference kinds are defined in `REF_KINDS` (code, not schema) because the prefixes are fixed Bannerlord conventions. The vanilla-culture floor set (`VANILLA_CULTURES`) backstops cultures that exist only in code/XSLT output.

## Coverage boundary — engine XSD rules are NOT checked

The validator checks **cross-references** (the `Item.` / `NPCCharacter.` / `Culture.` / `PartyTemplate.` prefixes, on any attribute in any `ModuleData/**/*.xml` — including `characters/clans.xml`) plus **per-schema** duplicate-id / enum / civilian-type rules. It does **not** validate the engine's own XSD schemas (`<game>/XmlSchemas/*.xsd`), which enforce *required-attribute presence* and element structure.

Concretely, `characters/clans.xml` has **no validator schema** (`tools/schemas/` covers only equipmentsets / npccharacter / spcultures). A clan (`<Faction>`) missing a `Factions.xsd`-required attribute such as `initial_home_settlement` therefore passes both `validate_moduledata.py` and the pre-commit hook, and surfaces only when the engine/editor loads the file (`Error: The required attribute 'initial_home_settlement' is missing. Node: Faction`). This is how `clan_umbar_3` shipped without a home settlement (fixed 2026-06-22). When authoring or editing `clans.xml`, open the file in the Bannerlord editor (or rely on an engine load) to catch required-attribute violations — the TAOM validator will not. A future improvement would be a `taom_factions.json` schema, but presence-of-required-attribute is not currently in the validator's model.

The same boundary applies in the other direction — an **extra, XSD-undeclared attribute** — even for files the validator *does* schema. `validate_moduledata.py` checks declared field types, enums, and cross-refs but does not reject unknown attributes; the engine's `NPCCharacters.xsd` does. So `npcs_rohan.xml` passed the validator (all 4,522 NPCCharacters parse) while the editor flagged a bogus `child_monster="…"` on its 10 child-template blocks (`Error: The 'child_monster' attribute is not declared`, fixed 2026-06-22). A clean `validate_moduledata.py` is necessary but not sufficient — the Bannerlord editor / an engine load is the authority for the XSD layer (both required-attribute presence *and* no-undeclared-attributes). Decide a flagged attribute via the decompiled deserializer + vanilla usage: bogus → remove (`child_monster`), real-but-XSD-incomplete → keep (`family_type` on horses, which `Items.xsd` likewise fails to declare).

## Key Files

| File | Purpose |
|---|---|
| `tools/taom_schema.py` | Engine (issue model, registries, schema model, `Validator`, `build_registries`, report) |
| `tools/taom_query.py` | Query API over the engine (`item_exists` / `troop_exists` / `culture_exists` / `find_references` / `validate` / listings) — backs the MCP server, pure stdlib |
| `tools/validate_moduledata.py` | CLI front-end (batch report) |
| `tools/taom_mcp_server.py` | MCP stdio server front-end (9 tools via the `mcp` SDK / FastMCP) |
| `tools/tests/test_taom_mcp_server.py` | in-process MCP server tests (skip if `mcp` SDK absent) |
| `tools/schemas/taom_npccharacter.json` | Troops + characters + wanderers + companions + education templates |
| `tools/schemas/taom_spcultures.json` | Cultures |
| `tools/schemas/taom_equipmentsets.json` | Equipment rosters (all `equipmentsets/*.xml`) |
| `tools/tests/test_validate_moduledata.py` | 24 unittest cases (validator) |
| `tools/tests/test_taom_query.py` | unittest cases (query API) |
| `.claude/hooks/check-moduledata-validation.sh` | PreToolUse commit gate (blocks on ERROR; fail-open) |
| `.claude/rules/moduledata-validation.md` | Auto-loaded rule when editing the covered XML / schemas |

## Dependencies

The **engine + query API + CLI** use the **Python 3 standard library only** (`re`, `json`, `glob`, `fnmatch`, `dataclasses`, `enum`, `pathlib`) — no pip install. The **MCP server** additionally needs the **`mcp` Python SDK** (FastMCP); it is present in this environment. Registries are built from the installed game at `E:\Steam\...\Modules` (override with `--game-modules`, or the `BANNERLORD_GAME_MODULES` env var for the MCP server) + `Main/_Module/ModuleData`.

## Tests

```bash
python -m unittest discover -s tools/tests -p "test_*.py"
```

`test_validate_moduledata.py` — 24 cases, one per issue code (positive + negatives) plus edge cases (Item.None allowed, malformed XML doesn't crash, file matching no schema is still swept, fail-fast on unknown rule / missing field, the Codex-fix regressions: culture-registry pollution, comment-stripping, child-template civilian, education-template exclusion). `test_taom_query.py` — the query API (existence checks incl. prefix/sentinel/duplicate, `find_references` with line numbers + comment-stripping, `validate` counts + code filter, listings). `test_taom_mcp_server.py` — in-process MCP tests (`list_tools()` returns all 9 tools; `call_tool()` for culture/schemas/registry/validate; install-independent, skips if the `mcp` SDK is absent). Full suite: **63 tests** (38 for this feature + 25 pre-existing weapon-xml).

## How-To

```bash
# Full validation against the installed game registry
python tools/validate_moduledata.py

# Only one check, write machine-readable output, treat warnings as errors
python tools/validate_moduledata.py --code BROKEN_ITEM_REF --json report.json --warnings-as-errors

# On a machine without the game install (item/troop/party checks auto-skip)
python tools/validate_moduledata.py --game-modules /nonexistent
```

**To add a new check:** prefer adding it to a schema (`enums`, or a new `special_rules` handler registered in `KNOWN_SPECIAL_RULES` + a `_*_rule` method). New cross-reference prefixes go in `REF_KINDS`. Add a test for the new issue code (positive + negative) — the suite is expected to cover every code the engine can emit.

## MCP server (interactive querying)

`tools/taom_mcp_server.py` is a stdio MCP server (FastMCP) that exposes the query API as tools, so a Claude agent can check mod-data integrity mid-task instead of grep-and-hope. Nine tools:

| Tool | Returns |
|---|---|
| `validate_moduledata(codes?)` | `{error_count, warning_count, issues[]}` — the full validation, optionally filtered to specific codes |
| `item_exists(item_id)` | `{id, exists, duplicate_in[]}` (bare or `Item.`-prefixed; `duplicate_in` non-empty if defined in >1 Armory folder) |
| `troop_exists(troop_id)` | `{id, exists}` |
| `culture_exists(culture_id)` | `{id, exists}` (e.g. `rohan` → false; the StringId is `vlandia`) |
| `party_template_exists(template_id)` | `{id, exists}` (bare or `PartyTemplate.`-prefixed) |
| `find_references(target_id, kind?, limit?)` | `{target, kind, count, truncated, references[{file,line,kind,ref}]}` (`kind` ∈ item/troop/culture/party_template, `npccharacter` aliased to troop; default `limit` 200) |
| `list_cultures()` | every valid culture StringId |
| `registry_sizes()` | `{items, npccharacters, cultures, party_templates}` — confirms the game install was found |
| `list_schemas()` | the schemas + what each checks |

**Activation** (one-time; the server can't be loaded mid-session — Claude reads MCP config at startup):
1. Ensure the `mcp` Python SDK is installed (`python -c "import mcp.server.fastmcp"` — present in this environment).
2. It is registered in [`.mcp.json`](../../.mcp.json) as the `taom-moduledata` stdio server and enabled in [`.claude/settings.local.json`](../../.claude/settings.local.json) → `enabledMcpjsonServers`.
3. **Restart Claude Code** to load it. Its tools then appear as `mcp__taom-moduledata__*` (deferred — schemas fetched via ToolSearch on demand).

**Smoke-test standalone** (no restart needed): `python tools/taom_mcp_server.py` starts the stdio server; or verify in-process:
```python
import asyncio, taom_mcp_server as srv
asyncio.run(srv.mcp.list_tools())          # 9 tools
asyncio.run(srv.mcp.call_tool("culture_exists", {"culture_id": "rohan"}))  # exists=False
```

The server resolves data paths from its own location, so it is cwd-independent; the game-modules path comes from `BANNERLORD_GAME_MODULES` (env) or the default Steam path, and degrades to TAOM-only registries if absent (same as the CLI).

## Performance

One pass over `Main/_Module/ModuleData/**/*.xml` (regex, line-numbered) + a registry build that scans the game-module item/character/culture/party-template XML once. Full live run completes in a few seconds; ~27k item refs + ~2.9k troop refs resolved per run.

## Known Scope (intentional)

Out of scope for v1 (documented as coverage gaps, not bugs): armor `covers_legs`/`covers_hands` (Armory-side schema), `BodyProperty.` refs, weapon-craft piece refs, scene refs (covered by `audit_scene_names.py`), and inline `EquipmentSet`-by-id refs to vanilla rosters.

NPC duplicate-id + enum coverage spans `troops/`, `characters/`, `named_companions/`, `taom_wanderers.xml`, and `taom_education_character_templates.xml` (the `taom_npccharacter.json` `applies_to` set — **add any new `<NPCCharacter>`-defining file there** or its dup/enum checks won't run; Codex review 2026-05-30 caught three uncovered files). The civilian-type rule treats `_civ*` and `child_template_*` rosters as civilian and checks every `<EquipmentSet>`, but **deliberately excludes** `child_education_*` education templates (0/784 are `Civilian`-tagged in real data — an unconfirmed convention; flagging them would be 784 false positives — confirm the convention before extending the rule).

## Changelog

- 2026-05-30 — Initial schema-driven ModuleData cross-reference validator: unified `taom_schema.py` engine + `validate_moduledata.py` CLI + 3 schemas catching the recurring bug classes (broken item/troop/culture/party-template refs, duplicate ids, missing civilian type, invalid enum); wired in as an auto-loaded scoped rule + a commit-blocking PreToolUse hook. Same dated entry covers the 2026-05-31 follow-up: the `taom_query.py` query API + `taom_mcp_server.py` MCP server (9 tools) and a second deep-review pass. See repo-root `CHANGELOG.md` for full detail.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by


<!-- backlinks-end -->
