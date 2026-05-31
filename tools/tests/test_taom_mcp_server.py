#!/usr/bin/env python3
"""In-process tests for the TAOM ModuleData MCP server (tools/taom_mcp_server.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

These exercise the FastMCP server the same way the stdio transport does
(`list_tools()` / `call_tool()`), without a Claude restart. They skip if the
`mcp` SDK isn't installed (it's an optional dependency of the server only).

Assertions are chosen to be game-install-INDEPENDENT (culture floor set +
repo-local schemas), so the suite passes in CI without the Bannerlord install.
Importing the server builds registries once against the real repo ModuleData.
"""
import asyncio
import json
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

try:
    import mcp.server.fastmcp  # noqa: F401
    _HAS_MCP = True
except Exception:
    _HAS_MCP = False

EXPECTED_TOOLS = {
    "validate_moduledata", "item_exists", "troop_exists", "culture_exists",
    "party_template_exists", "find_references", "list_cultures",
    "registry_sizes", "list_schemas",
}


@unittest.skipUnless(_HAS_MCP, "mcp SDK not installed")
class McpServerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        import taom_mcp_server as srv  # imports => builds registries once
        cls.srv = srv

    def _call(self, name, args):
        res = asyncio.run(self.srv.mcp.call_tool(name, args))
        if isinstance(res, tuple):  # newer FastMCP returns (content, structured)
            res = res[0]
        parsed = []
        for c in res:
            t = getattr(c, "text", None)
            if t is None:
                continue
            try:
                parsed.append(json.loads(t))
            except Exception:
                parsed.append(t)
        return parsed[0] if len(parsed) == 1 else parsed

    def test_all_nine_tools_registered(self):
        tools = asyncio.run(self.srv.mcp.list_tools())
        self.assertEqual({t.name for t in tools}, EXPECTED_TOOLS)

    def test_culture_exists_tool(self):
        # Install-independent: vlandia is in the vanilla-culture floor; rohan is not a StringId.
        self.assertTrue(self._call("culture_exists", {"culture_id": "vlandia"})["exists"])
        self.assertFalse(self._call("culture_exists", {"culture_id": "rohan"})["exists"])

    def test_list_schemas_tool(self):
        names = {s["name"] for s in self._call("list_schemas", {})}
        self.assertIn("taom_npccharacter", names)

    def test_registry_sizes_tool_keys(self):
        self.assertEqual(set(self._call("registry_sizes", {})),
                         {"items", "npccharacters", "cultures", "party_templates"})

    def test_validate_tool_returns_shape(self):
        out = self._call("validate_moduledata", {"codes": ["UNKNOWN_CULTURE"]})
        self.assertEqual(set(out), {"error_count", "warning_count", "issues"})


if __name__ == "__main__":
    unittest.main(verbosity=2)
