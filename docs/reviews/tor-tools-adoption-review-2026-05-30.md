# TOR_Tools Adoption Review (2026-05-30)

Comprehensive review of **[TheOldRealms/TOR_Tools](https://github.com/TheOldRealms/TOR_Tools)** — the content-tooling suite for "The Old Realms" (Warhammer Fantasy) Bannerlord total conversion — to extract workflow / tooling / process improvements for TAOM.

**Method:** exhaustive read-only review. Every file in the repo was accounted for via a 7-agent fan-out (one agent per subsystem + a synthesis pass), each enforcing a per-file coverage contract (every source file / schema / script / doc read in full; only large machine-generated data dumps characterized; only binary build stubs skipped — each with a stated reason). One agent (MCP-server) failed to return structured output and was re-read directly. Findings below are grounded in files actually read, not inferred.

**License:** MIT. Everything here is an **idea/architecture adoption** — we port patterns into TAOM's own (Python/C#) world; we do not vendor or install TOR code or binaries.

---

## 1. What TOR_Tools actually is

Not a mod — a **cross-platform C# desktop XML editor** (Avalonia UI 11 / .NET 8–10, CommunityToolkit.Mvvm) plus a **stdio MCP server** and **26 JSON schemas**, for editing the TOR mod's Bannerlord ModuleData. Target users: "semi-technical content creators." Its governing rule is *"Schemas are the source of truth; never hardcode schema knowledge in C#,"* alongside byte-faithful XML formatting preservation and a "huge files (`tor_strings.xml`) are MCP-query-only, never hand-edited" discipline.

| Subsystem | What it is | TAOM relevance |
|---|---|---|
| **26 JSON schemas** (`schemas/*.json`) | Per-XML-type field defs: type, enum, required, min/max, cross-reference targets, display metadata, prefix transforms, nested-path syntax, multi-file composition (linked/merged/additional-source) | TAOM has **zero** formal schema for its XML — agents guess field/enum/ref shapes |
| **Validation + CrossReference engine** (`Core/Validation/*`, `Core/Services/CrossReferenceService.cs` + 5 catalog services) | Schema-driven field validation + forward/reverse id→reference graph + per-file id catalogs with `exists()` | TAOM uses scattered one-shot Python validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, …) |
| **MCP server** (`TORTools.Mcp.Host`, ModelContextProtocol SDK, stdio) | 12 tools: `list_files`, `get_schema`, `query_entries`, `describe_file`, `distinct_values`, `aggregate`, `search`, `find_references`, `validate_entries`, + entry CRUD | TAOM's `.mcp.json` wires Serena/GitHub/filesystem/ilspy but **nothing mod-data-aware** |
| **Translation system** (`Core/Services/Translation/*` + MCP `StringsTools`/`TranslationTools`) | Per-string status enum (Translated/Todo/Missing/Orphaned), string categorization (50+ pattern rules), language-data template generator, JSON change cache, in-memory query-only index for the huge strings file | TAOM's 12-language Python pipeline lacks status tracking + categorization |
| **Byte-faithful XML I/O + undo/redo + git-value + workspace discovery** (`Core/Services/XmlDocumentService.cs`, `GitValueService.cs`, `Commands/*`, `Workspace/*`) | Detect-and-preserve indentation/BOM/encoding; atomic temp+move writes; `IEditCommand` undo/redo; `git show HEAD:` per-value diffing; neighboring-repo auto-discovery | TAOM's Python tools edit XML ad hoc (regex line edits), have been bitten by `re.sub` backref bugs, no undo |
| **Avalonia desktop UI + 3D preview** (`App/**`, `OpenGLViewport.cs`, `FbxLoaderService.cs`) | DataGrid editing, cross-ref click-through, validation-severity cell coloring, Silk.NET OpenGL weapon-parts 3D preview | Toolkit-specific; **not** directly adoptable (TAOM is Python-first, no desktop app) |
| **Lean process/config** (`.claude/CLAUDE.md` ~5KB) | 8-phase staged roadmap; schemas-as-truth + formatting-preservation + query-only-huge-files rules; external planning KB (`TORTasks/` with mermaid arch/dataflow); 23-entry permission allowlist | Contrast to TAOM's much larger harness |

---

## 2. Tiered adoption map

Security/licensing verdict: **MIT, idea-only port — no risk.** No binaries, no install; the C# Avalonia GUI itself is not portable to TAOM's Python-first stack. Adoption is limited to architecture, validation rules, and schema structure. Critical caveat (from the synthesis risk analysis, **confirmed true in practice**): TOR's schemas are tightly coupled to TOR's XML — TAOM schemas **must be derived from TAOM's actual XML**, not copied. (Verified: TAOM's culture set includes minor/bandit cultures — `gondor_soldiers`, `dunland_raiders`, … — absent from the hardcoded `xml-data.md` list; only a live scan finds them.)

### Tier 1 — adopt now
1. **Minimal JSON-schema layer + Python validator for the churn-heavy XML (cultures / troops / lords / equipmentsets).** Kills duplicate-id, missing-equipment-type, stale-culture-ref, dead-troop-ref, and missing-`covers_*` bug classes in one consolidated, reusable layer. **→ IMPLEMENTED this session (see §3).**
2. **Schema-driven cross-reference validation replacing the scattered one-shot validators.** One engine, declarative schemas, severity-classified output, exit-code gate for CI/pre-commit. **→ IMPLEMENTED this session.**
3. **Per-string translation status enum (Translated/Todo/Missing/Orphaned)** in the localization cache — surfaces orphaned strings after source refactors, lets translators filter TODOs. Low effort. **→ deferred (recommended, see §4).**
4. **Atomic temp-file + byte-faithful XML write utility** (`tools/xml_utils.py`) for the data-mutating Python tools (`rebalance_*.py`, `apply_*.py`) — crash-safe, BOM/CRLF-preserving. **→ deferred (recommended).**
5. **Formalize "schemas are source of truth" as a TAOM convention** — partially realized by the new `tools/schemas/` + the RCA memory; a full `.claude/rules/` card is optional.

### Tier 2 — worth a prototype
- **A TAOM MCP server** wrapping the validation engine + id catalogs (the flashiest TOR idea). **→ BUILT this session** (`tools/taom_mcp_server.py`, 9 stdio tools via FastMCP, registered as `taom-moduledata`). The earlier "net-version" deferral was wrong: built in **Python** wrapping the engine, the .NET concern doesn't apply. Protocol-tested in-process (`list_tools`/`call_tool`); needs a Claude restart to activate Claude-side discovery. See §3.
- **String categorization service** + category metadata for context-aware translation.
- **Language-data template generator + validate/repair** for new-language scaffolding.
- **Git-aware baseline diffing** (`git show HEAD:`) so audits distinguish "new in this branch" from "missing since HEAD."

### Tier 3 — note only
- Mermaid architecture/dataflow diagrams + a lightweight phase roadmap doc.
- Permission-allowlist hygiene audit (TOR's 23 tightly-scoped entries vs TAOM's ~45).
- Reverse cross-references ("what references this id?") once schemas exist — the engine already builds the data.
- Pytest harness for the data-mutating tools.

### Skip
- The Avalonia desktop GUI (toolkit-specific; TAOM has no business case for a desktop app yet).
- The 3D FBX/OpenGL weapon-parts preview (no Python OpenGL path; high effort, low immediate value).
- Full 26-schema adoption (TAOM's XML diverges; the minimal 4-type subset covers ~70% of pain at <50% effort).
- Direct C# port of the validation service (would add a .NET stack to Python-first tooling; the Python port is simpler).

---

## 3. What was implemented this session

The top two Tier-1 items **plus the Tier-2 MCP server** (built after the initial deferral — Python wrapping the engine, which dissolved the .NET-version concern). One engine, three front-ends (CLI / commit-hook / MCP) + discoverability wiring:

| File | Purpose |
|---|---|
| `tools/taom_schema.py` | Engine: `Issue`/`Severity` model, `Registries`, `Schema` (+ fail-fast load validation), prefix-based `REF_KINDS`, `Validator`, `build_registries`, `format_report` |
| `tools/taom_query.py` | Query API over the engine (existence checks, `find_references`, `validate`, listings) — pure stdlib, backs the MCP server |
| `tools/schemas/taom_{npccharacter,spcultures,equipmentsets}.json` | The three declarative schemas (source of truth); npccharacter glob covers troops/characters/companions/wanderers/education |
| `tools/validate_moduledata.py` | **CLI** front-end: severity report, `--json`, `--code`, `--warnings-as-errors`, exit 1 on ERROR |
| `tools/taom_mcp_server.py` | **MCP server** front-end: 9 stdio tools (FastMCP) — registered as `taom-moduledata` in `.mcp.json` |
| `.claude/hooks/check-moduledata-validation.sh` | **Pre-commit hook** front-end: blocks Claude-driven commits staging ModuleData XML with ERRORs (fail-open) |
| `.claude/rules/moduledata-validation.md` | Auto-loaded scoped rule when editing the covered XML / schemas |
| `tools/tests/test_validate_moduledata.py` (24) + `tools/tests/test_taom_query.py` | unittest suites (one case per issue code + edge cases + Codex-fix regressions + the query API) |

Checks: `BROKEN_ITEM_REF`, `BROKEN_TROOP_REF`, `UNKNOWN_CULTURE`, `DUPLICATE_NPC_ID`, `DUPLICATE_CULTURE_ID`, `DUPLICATE_ROSTER_ID`, `MISSING_CIVILIAN_TYPE`, `DUPLICATE_ITEM_DEF`, `INVALID_ENUM`, `BROKEN_PARTY_TEMPLATE_REF`. MCP tools: `validate_moduledata`, `item_exists`, `troop_exists`, `culture_exists`, `find_references`, `list_cultures`, `registry_sizes`, `list_schemas`. Full design: [moduledata-validation.md](../features/moduledata-validation.md).

**Verification (live, against the installed game registry):** PASS, 0 issues — corroborated by the existing `validate_all_troop_refs.py` (also PASS) and a positive-control instrument showing the engine sees and resolves **27,449 item refs / 2,869 troop refs / 3,957 culture refs / 136 party-template refs** with zero unresolved. See `docs/features/moduledata-validation.md` for design + usage.

**Reviews:** `/deep-review` (4 adapted agents) found 2 HIGH + 5 MED/LOW; `/review-codex` (gpt-5.5 xhigh) independently found 2 HIGH + 2 MED and correctly disputed all 8 weak suspects (0 false positives). **All 9 confirmed findings verified against source + fixed in-session** (24 tests pass; live PASS). The HIGH fixes raised troop-ref coverage 628→2,869 and cleaned culture-registry pollution (41→36 ids; all real refs still resolve). RCA: `docs/reviews/rca-moduledata-validation-2026-05-30.md`. Codex review: `docs/reviews/codex-adversarial-moduledata-validation-2026-05-30.md`. Notably both the deep-review and Codex HIGHs were the *same* over-broad-scan/coverage-gap pattern — captured as a new memory + RCA root-cause.

---

## 4. Next steps

**Done this session:**
- ✅ **TAOM MCP server** wrapping the engine (`taom_mcp_server.py`, 9 tools) — *was* item 1; built in Python. Needs a one-time Claude restart to activate.
- ✅ **Pre-commit hook** gating commits on ERROR-severity findings (`.claude/hooks/check-moduledata-validation.sh`) — *was* item 2.
- ✅ **Discoverability** — CLAUDE.md tool-index + MCP tables + Doc-Lookup, the scoped rule, and the hook.

**Still deferred (in priority order):**
1. **Activate the MCP server** — restart Claude so `taom-moduledata` loads (it's registered + enabled; only the restart is outstanding).
2. **Extend coverage**: armor `covers_legs`/`covers_hands` schema checks (Armory-side gap), `BodyProperty.` refs, weapon-craft piece refs, the `child_education_*` civilian convention (currently excluded to avoid false positives — confirm then enforce).
3. **Per-string translation status enum** + categorization in `translate_with_claude.py` (Tier 1 #3 / Tier 2).
4. **`tools/xml_utils.py`** atomic byte-faithful write helper, adopted by the `apply_*`/`rebalance_*` scripts (Tier 1 #4).
5. **CI**: run `python tools/validate_moduledata.py --warnings-as-errors` in `.github/workflows/` so broken refs fail PRs from any machine (the hook only covers Claude-driven local commits).

---

## Appendix: full agent findings

The complete 7-subsystem structured findings (schemas, validation-crossref, translation, core-io-workspace-git, app-ui-3d, process-config-data) + synthesis are preserved in the workflow result. Key per-subsystem adoptables and risks fed directly into the tier map above. The MCP tool catalog (read directly after the agent gap) is in §1.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/moduledata-validation.md](../features/moduledata-validation.md)

<!-- backlinks-end -->
