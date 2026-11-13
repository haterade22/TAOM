import ast
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[2]


class PetikCompatibilityTests(unittest.TestCase):
    def test_patch_refresh_uses_instance_context(self):
        source = (ROOT / "tools/petik/Petik/op_md_petik.py").read_text(encoding="utf-8")
        tree = ast.parse(source)
        patch = next(node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == "Patch")
        refresh = next(node for node in patch.body if isinstance(node, ast.FunctionDef) and node.name == "refresh")
        calls = [
            node for node in ast.walk(refresh)
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "PatchNode"
        ]
        self.assertEqual(1, len(calls))
        context = calls[0].args[0]
        self.assertIsInstance(context, ast.Attribute)
        self.assertEqual("self", context.value.id)
        self.assertEqual("context", context.attr)

    def test_menu_references_the_registered_multi_patch_operator(self):
        source = (ROOT / "tools/petik/Petik/op_md_petik.py").read_text(encoding="utf-8")
        self.assertNotIn("MdUtilPetik.bl_idname", source)
        self.assertIn("MdUtilMultiPetik.bl_idname", source)

    def test_reload_imports_modules_before_reloading_them(self):
        source = (ROOT / "tools/petik/Petik/__init__.py").read_text(encoding="utf-8")
        import_position = source.index("from . import (")
        self.assertLess(import_position, source.index("importlib.reload(op_md_petik)"))
        self.assertIn("importlib.reload(petik_preferences)", source)


if __name__ == "__main__":
    unittest.main()
