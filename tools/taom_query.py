#!/usr/bin/env python3
"""Query API over the TAOM ModuleData validation engine (tools/taom_schema.py).

Pure-stdlib, importable, unit-tested. Backs the MCP server
(tools/taom_mcp_server.py) and is usable directly from other tooling. Decoupled
from the game install via an injected Registries object (same pattern as the
validator), so it is testable with synthetic data.

This turns the batch validator into granular queries an agent can make mid-task
("does Item.X exist?", "what references troop Y?", "what are the valid culture
ids?") instead of grep-and-hope. See docs/features/moduledata-validation.md.
"""
from __future__ import annotations

import re
from pathlib import Path

import taom_schema as ts

# Prefix that identifies each reference kind in Bannerlord XML.
_PREFIXES = {
    "item": "Item.",
    "troop": "NPCCharacter.",
    "culture": "Culture.",
    "party_template": "PartyTemplate.",
}

# The engine (taom_schema.REF_KINDS) names the troop kind "npccharacter"; the
# query kind is "troop". Accept either so a caller using the REF_KINDS name
# doesn't silently fall back to an all-prefix search (deep-review 2026-05-31).
_KIND_ALIASES = {"npccharacter": "troop"}


class ModuleDataQuery:
    """Granular queries over the injected registries + the ModuleData tree."""

    def __init__(self, moduledata, registries: ts.Registries, schemas=None):
        self.moduledata = Path(moduledata)
        self.reg = registries
        self.schemas = list(schemas or [])

    # -- existence -------------------------------------------------------- #
    @staticmethod
    def _strip(value: str, prefix: str) -> str:
        return value[len(prefix):] if value.startswith(prefix) else value

    def item_exists(self, item_id: str) -> dict:
        bare = self._strip(item_id, "Item.")
        return {
            "id": bare,
            "exists": bare in self.reg.items or bare == "None",
            "duplicate_in": sorted(set(self.reg.item_def_files.get(bare, []))),
        }

    def troop_exists(self, troop_id: str) -> dict:
        bare = self._strip(troop_id, "NPCCharacter.")
        return {"id": bare, "exists": bare in self.reg.npccharacters}

    def culture_exists(self, culture_id: str) -> dict:
        bare = self._strip(culture_id, "Culture.")
        return {"id": bare, "exists": bare in self.reg.cultures}

    def party_template_exists(self, pt_id: str) -> dict:
        bare = self._strip(pt_id, "PartyTemplate.")
        return {"id": bare, "exists": bare in self.reg.party_templates}

    # -- listings --------------------------------------------------------- #
    def list_cultures(self) -> list:
        return sorted(self.reg.cultures)

    def registry_sizes(self) -> dict:
        return {
            "items": len(self.reg.items),
            "npccharacters": len(self.reg.npccharacters),
            "cultures": len(self.reg.cultures),
            "party_templates": len(self.reg.party_templates),
        }

    def list_schemas(self) -> list:
        return [{"name": s.name, "entry_element": s.entry_element,
                 "applies_to": s.applies_to, "description": s.description}
                for s in self.schemas]

    # -- references ------------------------------------------------------- #
    def _rel(self, xml: Path) -> str:
        try:
            return xml.relative_to(self.moduledata).as_posix()
        except ValueError:
            return xml.as_posix()

    def find_references(self, target_id: str, kind: str | None = None, limit: int = 200) -> dict:
        """Find every reference to an id across ModuleData. `target_id` may be
        prefixed (Item.x / NPCCharacter.x / Culture.x / PartyTemplate.x) or bare
        with `kind` set; with neither, all four prefixes are searched."""
        bare, inferred = target_id, _KIND_ALIASES.get(kind, kind)
        for k, p in _PREFIXES.items():
            if target_id.startswith(p):
                bare, inferred = target_id[len(p):], k
                break
        kinds = [inferred] if inferred in _PREFIXES else list(_PREFIXES)
        patterns = [(k, re.compile(r'="' + re.escape(_PREFIXES[k] + bare) + r'"')) for k in kinds]
        refs = []
        for xml in sorted(self.moduledata.rglob("*.xml")):
            text = ts._read_stripped(xml)
            rel = self._rel(xml)
            for k, pat in patterns:
                for m in pat.finditer(text):
                    refs.append({"file": rel, "line": ts._lineno(text, m.start()),
                                 "kind": k, "ref": _PREFIXES[k] + bare})
                    if len(refs) >= limit:
                        return {"target": bare, "kind": inferred, "count": len(refs),
                                "truncated": True, "references": refs}
        return {"target": bare, "kind": inferred, "count": len(refs),
                "truncated": False, "references": refs}

    # -- validation ------------------------------------------------------- #
    def validate(self, codes=None) -> dict:
        issues = ts.Validator(self.moduledata, self.schemas, self.reg).run()
        if codes:
            wanted = set(codes)
            issues = [i for i in issues if i.code in wanted]
        return {
            "error_count": sum(1 for i in issues if i.severity is ts.Severity.ERROR),
            "warning_count": sum(1 for i in issues if i.severity is ts.Severity.WARNING),
            "issues": [{"severity": i.severity.value, "code": i.code, "file": i.file,
                        "line": i.line, "entry_id": i.entry_id, "message": i.message}
                       for i in issues],
        }
