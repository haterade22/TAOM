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
- **Harness with no `family_type`** — defaults to 0 (human family), so the inventory screen refuses it on every mount with no message → "this item is not equipable". (`MISSING_HARNESS_FAMILY_TYPE`, `HARNESS_FAMILY_MISMATCH`)
- Duplicate NPC/culture/roster ids; invalid `default_group`; broken party-template refs.
- **`face_key_template` pointing at an undefined `BodyProperty`** — not an XML error: the engine
  registers a placeholder, `MBObjectManager.UnregisterNonReadyObjects` drops it, and the character
  silently loses its authored face. (`BROKEN_BODY_PROPERTY_REF`)
- **A lord's culture owns no settlement** — vanilla `SpawnLordParty` ends with an unguarded
  `Settlement.All.First(x => x.Culture == hero.Culture)`, so a landless culture on an
  `Occupation.Lord` hero is a latent `InvalidOperationException` on the daily clan tick.
  (`LANDLESS_CULTURE`)
- **A dwarf authored as cavalry, or handed a mount** — the dwarf skeleton's rider bone is
  misaligned, so a mounted dwarf spawns inside the horse mesh. (`MOUNTED_DWARF`)

"Schemas are the source of truth": field/enum/ref knowledge lives in `tools/schemas/*.json`, not hardcoded in Python.

## Architecture

```
tools/schemas/*.json   (declarative source of truth: entry element, id, enums, special rules)
        │  load_schemas()
        ▼
tools/taom_schema.py   ── build_registries(moduledata, game_modules)
   Registries (items, item_def_files, npccharacters, cultures, party_templates,
               body_properties, settled_cultures, suspect_registries)            ← injected (testable)
   REF_KINDS (prefix-based, attribute-agnostic)
   Validator.run():
     pass 1  global cross-reference sweep  (every *.xml under ModuleData
                                           + every extra_ref_root, e.g. LOTRLOME_Armory)
     pass 2  per-schema: duplicate-id, enum, civilian-type rule
     pass 3  duplicate item definitions across Armory folders
     pass 4b landless cultures (Lord NPCs / Factions / Kingdoms vs settled_cultures)
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

A third boundary, and the one that hid nine cultures' worth of Calradian troops for months: **the ref
sweep can only prove a reference RESOLVES, never that it points at the right thing, and it never reads
`spcultures.xslt` at all.** `settlement_patrol_template_level_1="PartyTemplate.patrol_party_empire_template_level_1"`
resolves perfectly, because vanilla ships that template; it is simply Calradian. And the XSLT is a
stylesheet rather than a data file, so a binding that lives only there is outside everything the
validator walks, as is a binding that is *absent* and inherited through the passthrough.

`PASS` from this validator therefore says nothing about whether a culture fields its own troops. That
question is owned by `TAOM.Tests/Core/CulturePartyTemplateTests.cs`, which transforms the stylesheet
and checks each emitted binding against the set of ids TAOM authors. The two are complementary: the
validator catches a typo'd id, the test catches a real id that belongs to the wrong faction. Run both.

## Landless-culture check (`LANDLESS_CULTURE`)

**Severity ERROR.** Fires when a culture carried by an `NPCCharacter` with `occupation="Lord"`, a
`<Faction>` or a `<Kingdom>` owns **no settlement in the world the game actually builds**.

Vanilla `HeroSpawnCampaignBehavior.SpawnLordParty` ends with an unguarded
`Settlement.All.First(x => x.Culture == hero.Culture)`, reached whenever the hero's map faction has
no `InitialHomeSettlement`. Vanilla never throws there because every Calradian culture owns land;
TAOM can, because `TAOM_Map/ModuleData/settlements.xslt` deletes every vanilla settlement and its
988 replacements cover only part of the 38 defined cultures (27 before the Khand retag, 28 after).
That is crash `099f650c` (2026-08-04) — `InvalidOperationException: Sequence contains no matching
element` out of `Campaign.Tick`'s daily clan tick. `Patch65_LandlessCultureSpawnGuard` catches it at
runtime; this check catches it before it ships. Full analysis:
[`lord-spawn-guard.md`](./lord-spawn-guard.md) (#374).

Evidence: **18** `LANDLESS_CULTURE` errors before the retag (the 18 TAOM-authored Variag lords in
`characters/lords.xml`), `PASS: no validation issues found.` after.

## Settlement-economy floor check (`SETTLEMENT_ECONOMY_FLOOR`)

**Severity ERROR.** Fires when a settlement whose culture is named in
[`tools/settlement_economy_floor.json`](../../tools/settlement_economy_floor.json) sits below that
spec's `town` / `castle` / `hearth` floor in the world the game actually builds.

The 2026-08-14 faction-economy pass raised every fief of eight fief-starved cultures in the **LIVE**
`<game>/Modules/TAOM_Map/ModuleData/settlements.xml`. That module is unversioned, so a reinstall
reverts the whole pass and nothing in this repo would otherwise notice: the same class of silent
loss behind CLAUDE.md's "A fix in a dependency module" trap, which closed seven issues in the
2026-08-08 triage. The spec file is the single source of truth, read by both
`rebalance_settlement_prosperity.py` (which writes the floor) and this check (which verifies it), so
neither restates the numbers. Re-apply with:

```
python tools/rebalance_settlement_prosperity.py --culture-floor-file tools/settlement_economy_floor.json --apply
```

Three degraded states are deliberately distinguished, because each one used to read as a pass:

| State | Result |
|---|---|
| Registry unavailable (no game install) | silent; the CLI already exits 2 |
| Spec file missing, or declaring no floor/cultures | **ERROR**: a deleted spec disables the gate exactly when it is needed |
| Spec names a culture that owns no settlement | **ERROR**: a retag or a typo'd id leaves the gate covering nothing |

The floor is clamped to the same `PROSPERITY_CAP` / `HEARTH_CAP` the writer clamps to, imported
from the writer rather than restated. Without that, a spec value above a cap would demand a number
no `--apply` could ever produce and the gate would fail on every commit forever.

Evidence: adding a culture whose fiefs sit below the floor produced 4 errors and exit 1; the shipped
spec reports `PASS`. Pinned by `SettlementEconomyFloorTests` and `SettlementEconomyRegistryTests` in
`tools/tests/test_validate_moduledata.py`.

## Mounted-dwarf check (`MOUNTED_DWARF`)

**Severity ERROR.** Fires on an `NPCCharacter` with `race="dwarf"` that is either tagged
`default_group="Cavalry"`/`"HorseArcher"`, or can reach a mount — a `slot="Horse"` entry in its own
inline `<EquipmentRoster>`, or in a standalone `<EquipmentRoster>` it names via
`<EquipmentSet id="…"/>`.

Dwarves use a custom, shorter skeleton whose rider bone is misaligned, so a mounted dwarf spawns
*inside* the horse mesh. `Patch46_TournamentDwarfDismount` already strips Horse + HorseHarness from
dwarf tournament participants at runtime, keyed on race; this check is the data-layer half of the
same invariant, so a troop revamp or a copy-pasted roster cannot reintroduce the defect.

**The two halves are not interchangeable** (decompiled v1.4.7, 2026-08-04):

| | Troop (non-hero) | Lord / hero |
|---|---|---|
| `default_group` | **is** the battlefield formation (`BasicCharacterObject.GetFormationClass():543` returns `DefaultFormationClass`) | ignored for formation; drives party-screen icons, tooltips, `CharacterCode` previews |
| Horse in the equipment slot | the actual mount | **decides the formation on its own** — `CharacterObject.GetFormationClass():818-839` overrides the base and, when `IsHero`, reads only `BattleEquipment` (a `HasHorseComponent` item in `EquipmentIndex.Horse` → Cavalry; plus a bow/crossbow → HorseArcher) |

So `default_group="Infantry"` on a lord holding a horse buys nothing — the mount alone spawns him
mounted. Checking only the enum would have missed exactly that case.

**Scope.** Only mounts a dwarf *character* can reach. Culture-selected player rosters (character
creation, career starters) are deliberately out of scope: no `NPCCharacter` references them, and all
12 custom cultures ship the same 16-of-55 sumpter-horse template, so gating them would flag a shared
vanilla-parity pattern rather than a dwarf defect.

Evidence at introduction: **0** `MOUNTED_DWARF` issues across all 185 `race="dwarf"` characters
(169 Infantry, 16 Ranged) — the data already complied. Negative control: flipping `lord_E1_1` to
`default_group="Cavalry"` produced `1 error(s)` at `characters/lords.xml:9829`, then reverted.

### The `settled_cultures` registry (`build_settled_cultures`)

`build_settled_cultures(game_modules)` walks the settlement-contributing modules in SubModule load
order — `Native`, `SandBoxCore`, `SandBox`, `CustomBattle`, `TAOM_Map` — collecting every
`<Settlement … culture="Culture.X">` from each module's `settlements.xml` (`_SETTLEMENT_CULTURE_RE`).

**It honours the unconditional strip, and that is the load-bearing detail.** When a module's
`settlements.xslt` carries an empty `<xsl:template match="Settlement"/>` (TAOM_Map ships exactly
one), everything accumulated so far is discarded before that module's own settlements are added.
A registry built without that models a world the game never builds: it counts vanilla's 494 deleted
settlements, reports every culture as landed, and prints PASS while the game crashes. Both spellings
of the empty template (self-closing and empty-body) are matched by `_SETTLEMENT_STRIP_RE`.

Two guards match the other registries' behaviour: an **empty** `settled_cultures` (no game install)
skips the check entirely rather than reporting everything broken, and a **size floor of 15** feeds
`Registries.suspect_registries`, so a shrunken registry is named out loud instead of passing quietly.

### `_LANDLESS_BY_DESIGN` allowlist

The ten cultures still landless after the Khand retag are allowlisted, each with its reason in-code.
Adding an entry is a deliberate act — state why:

| Cultures | Why they cannot reach the throwing line |
|---|---|
| `looters`, `sea_raiders`, `mountain_bandits`, `forest_bandits`, `desert_bandits`, `steppe_bandits` | Bandit heroes are `Occupation.Bandit`; `GetBestAvailableCommander` filters on `Occupation.Lord`. |
| `neutral_culture` | Vanilla placeholder culture, carried by no TAOM lord or clan. |
| `darshi`, `nord`, `vakken` | Vanilla minor-faction cultures (ghilman / skolderbrotva / forest_people) TAOM inherits but never re-cultured. All three clans keep a valid `initial_home_settlement`, so vanilla never reaches the `First()`; Patch65 covers them if a mod re-parents their lords. |

### Scope: TAOM's own ModuleData only

The sweep reads `Main/_Module/ModuleData/**/*.xml`, matching the validator's documented contract. A
vanilla-inherited faction whose `InitialHomeSettlement` is null at runtime is Patch65's problem, not
a TAOM data defect — nothing in the files this validator owns is wrong in that case.

**Tests:** `tools/tests/test_validate_moduledata.py` carries two classes for this check.
`LandlessCultureTests` — `test_lord_in_landless_culture_is_reported`,
`test_lord_in_landed_culture_is_clean`, `test_clan_and_kingdom_in_landless_culture_are_reported`,
`test_non_lord_occupation_is_ignored`, `test_allowlisted_cultures_are_not_reported`,
`test_check_skipped_when_settlement_registry_unavailable`.
`SettledCultureRegistryTests` — `test_unconditional_strip_discards_earlier_modules`,
`test_without_a_strip_modules_merge`, `test_no_game_install_yields_empty_registry`,
`test_registry_is_wired_into_build_registries_and_floored`. The strip and merge cases differ only
by the presence of `settlements.xslt` and expect different sets, so they fail if the strip handling
regresses.

## Key Files

| File | Purpose |
|---|---|
| `tools/taom_schema.py` | Engine (issue model, registries, schema model, `Validator`, `build_registries`, `build_settled_cultures`, report) |
| `tools/taom_query.py` | Query API over the engine (`item_exists` / `troop_exists` / `culture_exists` / `find_references` / `validate` / listings) — backs the MCP server, pure stdlib |
| `tools/validate_moduledata.py` | CLI front-end (batch report) |
| `tools/taom_mcp_server.py` | MCP stdio server front-end (9 tools via the `mcp` SDK / FastMCP) |
| `tools/tests/test_taom_mcp_server.py` | in-process MCP server tests (skip if `mcp` SDK absent) |
| `tools/schemas/taom_npccharacter.json` | Troops + characters + wanderers + companions + education templates |
| `tools/schemas/taom_spcultures.json` | Cultures |
| `tools/schemas/taom_equipmentsets.json` | Equipment rosters (all `equipmentsets/*.xml`) |
| `tools/tests/test_validate_moduledata.py` | 75 unittest cases (validator) |
| `tools/tests/test_taom_query.py` | unittest cases (query API) |
| `.claude/hooks/check-moduledata-validation.sh` | PreToolUse commit gate (blocks on ERROR; fail-open) |
| `.claude/rules/moduledata-validation.md` | Auto-loaded rule when editing the covered XML / schemas |

## Dependencies

The **engine + query API + CLI** use the **Python 3 standard library only** (`re`, `json`, `glob`, `fnmatch`, `dataclasses`, `enum`, `pathlib`) — no pip install. The **MCP server** additionally needs the **`mcp` Python SDK** (FastMCP); it is present in this environment. Registries are built from the installed game at `E:\Steam\...\Modules` (override with `--game-modules`, or the `BANNERLORD_GAME_MODULES` env var for the MCP server) + `Main/_Module/ModuleData`.

## Tests

```bash
python -m unittest discover -s tools/tests -p "test_*.py"
```

`test_validate_moduledata.py` — 75 cases, one per issue code (positive + negatives) plus edge cases (Item.None allowed, malformed XML doesn't crash, file matching no schema is still swept, fail-fast on unknown rule / missing field, the Codex-fix regressions: culture-registry pollution, comment-stripping, child-template civilian, education-template exclusion, the harness family-type set: missing attribute, cross-set non-pairing, ambiguous monster ids, degraded mode, and the mounted-dwarf set: both rules, an absent `race`, the lowercase `<equipment>` spelling, and a non-dwarf positive control). `test_taom_query.py` — the query API (existence checks incl. prefix/sentinel/duplicate, `find_references` with line numbers + comment-stripping, `validate` counts + code filter, listings). `test_taom_mcp_server.py` — in-process MCP tests (`list_tools()` returns all 9 tools; `call_tool()` for culture/schemas/registry/validate; install-independent, skips if the `mcp` SDK is absent). Full suite: **297 tests** across 12 files in `tools/tests/`.

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

One pass over `Main/_Module/ModuleData/**/*.xml` **plus every `extra_ref_root`** (regex,
line-numbered) + a registry build that scans the game-module item/character/culture/party-template/
body-property XML once. 259 TAOM files + 382 Armory files = 641, each read once, patterns
pre-compiled. Full live run completes in a few seconds; ~27k item refs + ~2.9k troop refs resolved
per run.

## Known Scope (intentional)

Out of scope (documented as coverage gaps, not bugs): armor `covers_legs`/`covers_hands`
(Armory-side schema), weapon-craft piece refs, scene refs (covered by `audit_scene_names.py`), and
inline `EquipmentSet`-by-id refs to vanilla rosters. `BodyProperty.` refs **were** on this list and
are now checked (2026-08-03).

### Foreign-module sweep (`extra_ref_roots`)

The CLI passes `LOTRLOME_Armory/ModuleData` as an extra ref root. TAOM authors item XML directly
into that module (see `/author-armor`), so it is TAOM's to keep correct even though it lives
outside this repo and outside git. Extra roots are swept for **cross-references only** — the schema
contracts (duplicate ids, civilian `equipmentType`, enums) describe TAOM's own files and must not
report defects against a module this validator does not own.

Today that sweep is load-bearing for `Culture.` refs (104 Armory files carry them) and effectively
vacuous for the other four kinds — Armory XML currently contains no live `NPCCharacter.`,
`PartyTemplate.` or `BodyProperty.` refs, and its single `Item.` hit is inside a comment. The
wiring is correct and will catch a real dangling ref if one is introduced; just don't read a PASS
as proof those kinds were stress-tested against the Armory.

### Module coverage at a glance (and what is NOT covered)

TAOM's data spans **three modules**, and only one of them is in this repo. `TAOM_Map` and
`LOTRLOME_Armory` live in the game install and are unversioned, which is why CLAUDE.md's
"A fix in a dependency module" trap insists on an in-repo gate beside every external edit.
Counts measured 2026-08-18:

| Module | Location | XML (ModuleData / all) | XSLT | XML well-formedness | Cross-ref sweep | XSLT checked |
|---|---|---|---|---|---|---|
| TAOM | this repo | 259 / 338 | 8 | CI, `Main/_Module/ModuleData/**` | full (259 files), plus schema contracts | CI (8 of 8) + `check_external_xslt.py`; `/xslt-check` maps 6 of 8 |
| TAOM_Map | game install | 44 / 313 | 1 | `check_external_xslt.py` | full (44 files) since #462 | `check_external_xslt.py` |
| LOTRLOME_Armory | game install | 382 / 406 | 7 | `check_external_xslt.py` | **full (382 files)** via `extra_ref_roots` | `check_external_xslt.py` |
| total | | 685 / **1,057** | **16** | | **685 files swept** | **16 of 16** |

The two columns matter: the validator only ever reads `ModuleData`, so the left number is its
ceiling and the right one is the module's whole XML surface. Of TAOM's 259 ModuleData files, 145
are localization files under `Languages/` (12 language folders). The deployed `Modules/TAOM` copy is build output; the repo is
authoritative for it, so do not count it twice. Counts measured 2026-08-18; the 641 is
`len(_xml_files()) + len(_extra_ref_files())` on a live `Validator`.

**The Armory is in better shape than its "foreign module" status suggests.** All 382 of its
ModuleData XML are swept for dangling refs, and it contributes to two registries, not one: items via
`item_roots`, and `<Monster>` declarations via `build_harness_registries`, which feeds
`mount_family_types`. What it does *not* get is TAOM's schema contracts (duplicate ids, enums,
civilian `equipmentType`), because those describe TAOM's own files. That is currently free: the
Armory defines 3,727 items and 63 monsters and **zero** NPCCharacters, EquipmentRosters, party
templates, cultures or body properties, so the passes that skip it have nothing to miss. That is an
assumption about today's data, not a guarantee, and `/author-armor`'s workflow makes it plausible
someone authors a troop there. Worth an invariant test that fails loudly when it stops holding.

**`TAOM_Map` WAS the sharp gap, closed by #462.** Its ModuleData is now an `extra_ref_root`, so all
44 files and their 1,012 `Culture.` refs are swept; the validator visits 685 files (259 repo, 382
Armory, 44 TAOM_Map). What follows is the shape of the hole, kept because the registry asymmetry it
describes is still true and still the reason the sweep matters. Exactly **two**
of the 45 XML and XSLT files in its `ModuleData` are ever opened (44 XML plus `settlements.xslt`;
the directory holds 88 files in all, the other 43 being 39 `.bak*`/`.prev` copies, three
`DistanceCaches` outputs and `project.mbproj`), both inside `build_settled_cultures` and
`build_settlement_economy`: `settlements.xslt`, read only to evaluate one boolean regex, and
`settlements.xml`. TAOM_Map appears in no other root list, not `item_roots`, not `npc_roots`, not
`pt_roots`, not `culture_files`, and not `extra_ref_roots`.

The consequence is an inversion worth stating plainly. **The validator checks the dead file's refs
and trusts the live one's.** The repo's `Main/_Module/ModuleData/settlements.xml` is a stale shadow
that contributes to no registry, yet its `Culture.` refs are checked on every run. The live
`TAOM_Map/ModuleData/settlements.xml` is the *sole* source of `settled_cultures` and
`settlement_economy`, and its **1,012 `Culture.` references (30 distinct) are never checked for
`UNKNOWN_CULTURE`**. Since that same file is what decides which cultures count as settled, a bad id
there corrupts the `LANDLESS_CULTURE` verdict in one direction or the other with no diagnostic at
all. Verified 2026-08-18: all 30 currently resolve against the 40-entry culture registry, so the
gap is latent, not live.

**Fixed in #462.** `build_extra_ref_roots()` in `validate_moduledata.py` now returns both modules
from one `_EXTRA_REF_MODULES` tuple, and `ExtraRefRootTests` pins the contract in both directions,
including an end-to-end case where a bogus `Culture.` id in a synthetic TAOM_Map root must raise
`UNKNOWN_CULTURE`. The registry asymmetry above is unchanged: TAOM_Map is still the sole source of
`settled_cultures` and still contributes to no other registry. What changed is that its refs are no
longer taken on trust.

**XSLT is barely modelled anywhere.** `_SETTLEMENT_STRIP_RE` matches only an empty
`<xsl:template match="Settlement"/>` or its empty-body form; the file is never parsed as XML and
never transformed. If that live, unversioned stylesheet were malformed, or the strip were rewritten
to an equivalent the regex does not match (extra whitespace, a non-`xsl` prefix, an
identity-suppressing variant), the match returns nothing, vanilla's stripped settlements are counted
as live, and every culture reports as landed. That is precisely the false-clean `LANDLESS_CULTURE`
exists to prevent, and the tests use synthetic fixtures, so they pin the detector and never touch
the live file.

**The 8 external stylesheets had no validation path until #462.**
[`tools/check_external_xslt.py`](../../tools/check_external_xslt.py) now gates all 16 across the
three modules: XML well-formedness always, a root-element check (a stylesheet the engine will
silently ignore is worse than a broken one), and a real stylesheet compile when `lxml` is present.
It is a developer-side script by necessity, since CI cannot see the live modules. The limitation
below is why it exists. `/xslt-check` resolves its target
under `Main/_Module/ModuleData/`, and the CI `validate-xml` job globs that same repo path, so
neither reaches them; CI *structurally* cannot, because those modules are not in the checkout. Two
are read narrowly for unrelated purposes and both fail open: `audit_mount_parity.py` string-replaces
`action_sets.xml` to `action_sets.xslt` and regexes out chariot animations behind an `os.path.exists`
guard, and `weapon_xml/verify.py` regex-checks only the piece ids a given `build_weapon_xml.py` run
just generated, never pre-existing content. Note that `audit_action_set_parity.py`, which CLAUDE.md
names as the gate for the root-`<action>` dedicated-server hazard, reads `action_sets.xml` only and
contains no XSLT handling at all.

Treat this section as the answer to "is my change covered", not as a to-do list someone is working
through.

### Silent-scope guards

Two failure modes make an under-scoped run indistinguishable from a clean one, so both are reported
rather than inferred from the numbers:
- A **missing extra ref root** (renamed/moved Armory) is recorded in `Validator.missing_ref_roots`
  and the CLI prints a WARNING naming the skipped module. Silently dropping it would revert the
  sweep to TAOM-only while still printing PASS.
- A **shrunken registry** (a renamed vanilla `*_bodyproperties.xml`, a typo'd `--game-modules` that
  still resolves to a real directory) is flagged via `Registries.suspect_registries`. Floors are set
  far below real counts (121 body properties, 38 cultures) — they catch "the file list broke", not
  "the data changed a bit". Full shrinkage would otherwise trip the empty-registry guard and skip
  the check entirely.

NPC duplicate-id + enum coverage spans `troops/`, `characters/`, `named_companions/`, `taom_wanderers.xml`, and `taom_education_character_templates.xml` (the `taom_npccharacter.json` `applies_to` set — **add any new `<NPCCharacter>`-defining file there** or its dup/enum checks won't run; Codex review 2026-05-30 caught three uncovered files). The civilian-type rule treats `_civ*` and `child_template_*` rosters as civilian and checks every `<EquipmentSet>`, but **deliberately excludes** `child_education_*` education templates (0/784 are `Civilian`-tagged in real data — an unconfirmed convention; flagging them would be 784 false positives — confirm the convention before extending the rule).

## Changelog

- 2026-08-04 — Added `MOUNTED_DWARF` (pass 6). Asked to confirm no dwarven lord is cavalry, the
  audit found the data already compliant — 185 `race="dwarf"` characters, all Infantry or Ranged,
  no lord roster carrying a mount — so the work became pinning the invariant rather than fixing it.
  Checks both the `default_group` enum and reachable Horse slots, because decompiling v1.4.7 showed
  `CharacterObject.GetFormationClass()` ignores `default_group` for heroes and reads equipment
  instead: an enum-only check would pass a lord who still spawns mounted. `"erebor"` also dropped
  from `HORSE_CULTURES` in `tools/fix_lord_cultures_and_mounts.py`, which would otherwise have
  injected `Item.charger` into the Erebor lord rosters if that (currently broken) script were repaired.
- 2026-08-04 — Added `LANDLESS_CULTURE` (pass 4b) and the `settled_cultures` registry
  (`build_settled_cultures` — load-order walk that honours TAOM_Map's unconditional
  `<xsl:template match="Settlement"/>` strip, size floor 15). Came out of crash `099f650c`: TAOM's
  `battania` is the authored Variag culture, its K-series settlements were never migrated with it,
  and vanilla `SpawnLordParty`'s unguarded `Settlement.All.First(culture)` threw on the daily clan
  tick. 18 errors before the Khand settlement retag
  (`tools/oneoff/retag_khand_to_variag.py`), PASS after. Runtime guard:
  [`lord-spawn-guard.md`](./lord-spawn-guard.md) (#374).
- 2026-08-03 — Added `BROKEN_BODY_PROPERTY_REF` (registry from the 4 authoritative
  `*_bodyproperties.xml` files, 121 ids) and the `extra_ref_roots` foreign-module sweep, wired to
  `LOTRLOME_Armory/ModuleData`. Both came out of the dwarf-vs-Rhûn crash investigation, whose log
  showed three dangling refs this validator could not have caught
  (`docs/reviews/investigation-rhun-dwarf-ctd-2026-08-02.md`). The deep review then found the new
  sweep could skip silently and print PASS; `missing_ref_roots` + `suspect_registries` close that
  (`docs/reviews/rca-validator-silent-scope-2026-08-03.md`).
- 2026-05-30 — Initial schema-driven ModuleData cross-reference validator: unified `taom_schema.py` engine + `validate_moduledata.py` CLI + 3 schemas catching the recurring bug classes (broken item/troop/culture/party-template refs, duplicate ids, missing civilian type, invalid enum); wired in as an auto-loaded scoped rule + a commit-blocking PreToolUse hook. Same dated entry covers the 2026-05-31 follow-up: the `taom_query.py` query API + `taom_mcp_server.py` MCP server (9 tools) and a second deep-review pass. See repo-root `CHANGELOG.md` for full detail.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/doc-health-linter.md](./doc-health-linter.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/engine/formations-and-team-ai.md](../reference/engine/formations-and-team-ai.md)
- [docs/reviews/lessons/xslt-moduledata.md](../reviews/lessons/xslt-moduledata.md)
- [docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md](../reviews/rca-tournament-dwarf-dismount-2026-06-09.md)

<!-- backlinks-end -->
