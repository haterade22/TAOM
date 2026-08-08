#!/usr/bin/env python3
"""TAOM ModuleData MCP server (stdio).

Exposes the schema-driven validation engine + query API (tools/taom_schema.py,
tools/taom_query.py) as MCP tools, so Claude agents can query mod-data integrity
INTERACTIVELY mid-task -- "does Item.X exist?", "what references troop Y?", "what
are the valid culture ids?", "validate the ModuleData" -- instead of grep-and-hope.

It is the interactive counterpart to the batch CLI (tools/validate_moduledata.py)
and the pre-commit hook (.claude/hooks/check-moduledata-validation.sh); all three
share the same engine. The design follows a schema-driven validation engine whose
MCP host exposes the same shape of query/validate tools.

Requires the `mcp` Python SDK. Run standalone to smoke-test:
    python tools/taom_mcp_server.py
Register as a stdio MCP server in .mcp.json (command: python, args:
[tools/taom_mcp_server.py]) + enable in .claude/settings.local.json. See
docs/features/moduledata-validation.md "MCP server" for activation steps.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import taom_schema as ts
import taom_query as tq
from _gamedir import game_modules
from mcp.server.fastmcp import FastMCP

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULEDATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
SCHEMA_DIR = Path(__file__).resolve().parent / "schemas"
# $BANNERLORD_GAME_MODULES still wins when set, but the folder is otherwise
# derived from $BANNERLORD_GAME_DIR like every other tool: setting the install
# root correctly and still having this server answer against the old install is
# the trap #404 removes.
GAME_MODULES = game_modules(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")


def _build_query() -> tq.ModuleDataQuery:
    """Build registries once at startup (cached for the server's lifetime). When
    the game install is absent the item/troop/party registries are empty and the
    affected checks are skipped -- same degradation as the CLI.

    A malformed schema (load_schemas raises) or unreadable ModuleData fails
    fast: we print a clear diagnostic to stderr (surfaced in the MCP client's
    server log) and re-raise, rather than silently serving empty/wrong results."""
    try:
        schemas = ts.load_schemas(SCHEMA_DIR)
        registries = ts.build_registries(
            MODULEDATA, GAME_MODULES if GAME_MODULES.exists() else None)
        return tq.ModuleDataQuery(MODULEDATA, registries, schemas)
    except Exception as exc:  # noqa: BLE001 - startup diagnostic then re-raise
        print(f"[taom-moduledata] failed to initialize: {exc!r}\n"
              f"  SCHEMA_DIR={SCHEMA_DIR}\n  MODULEDATA={MODULEDATA}\n"
              f"  GAME_MODULES={GAME_MODULES} (exists={GAME_MODULES.exists()})",
              file=sys.stderr)
        raise


_query = _build_query()

mcp = FastMCP("taom-moduledata")


@mcp.tool()
def validate_moduledata(codes: list[str] | None = None) -> dict:
    """Validate all TAOM ModuleData XML against the schemas. Returns
    {error_count, warning_count, issues:[{severity, code, file, line, entry_id,
    message}]}. Optional `codes` filters to specific issue codes (e.g.
    ["BROKEN_ITEM_REF","UNKNOWN_CULTURE"])."""
    return _query.validate(codes)


@mcp.tool()
def item_exists(item_id: str) -> dict:
    """Does an item id resolve to a defined item? Accepts bare ("charger") or
    prefixed ("Item.charger"). Returns {id, exists, duplicate_in:[files]} where
    duplicate_in is non-empty if the id is defined in >1 LOTRLOME_items folder."""
    return _query.item_exists(item_id)


@mcp.tool()
def troop_exists(troop_id: str) -> dict:
    """Does an NPCCharacter id resolve to a defined troop/character? Accepts bare
    or "NPCCharacter."-prefixed. Returns {id, exists}."""
    return _query.troop_exists(troop_id)


@mcp.tool()
def culture_exists(culture_id: str) -> dict:
    """Is a culture id a valid StringId? Accepts bare or "Culture."-prefixed.
    Returns {id, exists}. (e.g. "rohan" -> False; the StringId is "vlandia".)"""
    return _query.culture_exists(culture_id)


@mcp.tool()
def party_template_exists(template_id: str) -> dict:
    """Does a party-template id resolve to a defined MBPartyTemplate? Accepts
    bare or "PartyTemplate."-prefixed. Returns {id, exists}."""
    return _query.party_template_exists(template_id)


@mcp.tool()
def find_references(target_id: str, kind: str | None = None, limit: int = 200) -> dict:
    """Find every reference to an id across ModuleData. `target_id` may be
    prefixed (Item./NPCCharacter./Culture./PartyTemplate.) or bare with `kind`
    set to one of item|troop|culture|party_template (npccharacter is accepted as
    an alias of troop). Returns {target, kind, count, truncated, references:
    [{file, line, kind, ref}]}, capped at `limit` (default 200)."""
    return _query.find_references(target_id, kind, limit)


@mcp.tool()
def list_cultures() -> list[str]:
    """List every valid culture StringId (custom + minor/bandit + vanilla floor)."""
    return _query.list_cultures()


@mcp.tool()
def registry_sizes() -> dict:
    """Counts of the indexed registries {items, npccharacters, cultures,
    party_templates} -- useful to confirm the game install was found."""
    return _query.registry_sizes()


@mcp.tool()
def list_schemas() -> list[dict]:
    """List the validation schemas {name, entry_element, applies_to, description}
    -- the source of truth for what is checked."""
    return _query.list_schemas()


if __name__ == "__main__":
    mcp.run()  # stdio transport (default)
