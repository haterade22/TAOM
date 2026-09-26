r"""insert_after_line must keep each file's own line terminator (tools/README.md XML I/O convention).

2026-09-26: its anchor matched `.*` to the end of the line, and on a CRLF line `.*` takes the `\r`, so the
hammer lines went in between `\r` and `\n`: three live Armory files gained one `\r\r\n` and one bare `\n`.
"""
import os
import re
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "oneoff"))
import add_hill_troll_hammer_items as h  # noqa: E402

ANCHOR = re.escape('<x id="cave"/>')


class InsertAfterLineTests(unittest.TestCase):
    def test_crlf_file_stays_crlf(self):
        text = '<a>\r\n\t<x id="cave"/>\r\n\t<y/>\r\n</a>\r\n'
        out = h.insert_after_line(text, ANCHOR, ['<x id="hill"/>', '<x id="hill2"/>'], "\r\n")
        self.assertEqual(out, '<a>\r\n\t<x id="cave"/>\r\n\t<x id="hill"/>\r\n\t<x id="hill2"/>\r\n\t<y/>\r\n</a>\r\n')

    def test_lf_file_stays_lf(self):
        text = '<a>\n  <x id="cave"/>\n  <y/>\n</a>\n'
        out = h.insert_after_line(text, ANCHOR, ['<x id="hill"/>'], "\n")
        self.assertEqual(out, '<a>\n  <x id="cave"/>\n  <x id="hill"/>\n  <y/>\n</a>\n')

    def test_anchor_on_an_unterminated_last_line(self):
        text = '<a>\r\n<x id="cave"/>'
        out = h.insert_after_line(text, ANCHOR, ['<x id="hill"/>'], "\r\n")
        self.assertEqual(out, '<a>\r\n<x id="cave"/>\r\n<x id="hill"/>')

    def test_an_anchor_matching_two_lines_is_refused(self):
        text = '<x id="cave"/>\r\n<x id="cave"/>\r\n'
        with self.assertRaises(SystemExit):
            h.insert_after_line(text, ANCHOR, ['<x id="hill"/>'], "\r\n")


def package(items):
    """A version-2 tpac holding the given (type_guid, name) items, TOC only, in the layout
    validate_mesh_refs.scan_tpac_metameshes reads (tests/test_validate_mesh_refs.py _build_one_item_tpac)."""
    import struct
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    import validate_mesh_refs as vm
    buf = bytearray(struct.pack("<I", vm.TPAC_MAGIC) + struct.pack("<I", 2) + b"\x11" * 16
                    + struct.pack("<I", len(items)) + struct.pack("<II", 0, 0))
    for type_guid, name in items:
        nb = name.encode("utf-8")
        buf += type_guid + b"\x22" * 16 + struct.pack("<I", 0) + struct.pack("<i", len(nb)) + nb
        buf += struct.pack("<qqii", 0, 0, 0, 0)
    return bytes(buf)


class PackageGapsTests(unittest.TestCase):
    """The --apply gate reads exact names from the package's table of contents. It used to test raw bytes, and the
    head mesh name is a substring of the body name, so a package holding only a mistyped body passed (#352 hang)."""

    def _gaps(self, items):
        import tempfile
        sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
        import validate_mesh_refs as vm
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "pkg.tpac")
            open(path, "wb").write(package([(vm.METAMESH_TYPE_GUID if kind == "mesh" else vm.PHYSICSSHAPE_TYPE_GUID,
                                              name) for kind, name in items]))
            return h.package_gaps(path)

    def test_the_exact_names_pass(self):
        self.assertEqual(self._gaps([("mesh", h.HEAD), ("mesh", h.HANDLE), ("body", h.BODY)]), [])

    def test_a_body_name_that_merely_contains_the_head_name_is_refused(self):
        gaps = self._gaps([("mesh", h.HANDLE), ("body", h.BODY + "_a")])
        self.assertIn(h.HEAD, gaps)
        self.assertIn(h.BODY, gaps)

    def test_a_body_shipped_as_a_mesh_is_refused(self):
        self.assertEqual(self._gaps([("mesh", h.HEAD), ("mesh", h.HANDLE), ("mesh", h.BODY)]), [h.BODY])

    def test_a_missing_package_is_refused(self):
        self.assertTrue(h.package_gaps(os.path.join(os.path.dirname(__file__), "no_such_package.tpac")))


CAVE_PIECES = ('<CraftingPieces>\r\n'
               '  <CraftingPiece id="wm_cave_troll_2h_mace_head" name="{=aom_x}X" mesh="m" length="50" weight="1.23">\r\n'
               '    <BladeData blade_length="50" body_name="bo_wm_cave_troll_2h_mace_head" />\r\n'
               '  </CraftingPiece>\r\n'
               '  <CraftingPiece id="wm_cave_troll_2h_mace_handle" name="{=aom_y}Y" mesh="h" length="258">\r\n'
               '    <BuildData piece_offset="70" />\r\n'
               '  </CraftingPiece>\r\n'
               '</CraftingPieces>\r\n')


class PieceTests(unittest.TestCase):
    def test_the_head_carries_the_weight_that_prices_it_at_the_mace(self):
        # the engine simulates damage and speed from piece geometry: the cave head's 1.23 on the hammer's longer
        # geometry priced at 23 Blunt / speed 12; 0.875 prices at the mace's 86 / 28 (2026-09-26)
        _, block = h.head_block(CAVE_PIECES)
        self.assertIn('weight="0.875"', block)
        self.assertNotIn('weight="1.23"', block)


class EndToEndTests(unittest.TestCase):
    """plan() over a temp copy of the six files: one apply adds the hammer, a second changes nothing, BOMs kept."""

    FILES = {
        "LOTRLOME_crafting_pieces.xml": CAVE_PIECES,
        "weapon_descriptions.xslt": '<x>\r\n\t<AvailablePiece id="wm_cave_troll_2h_mace_handle"/>\r\n</x>\r\n',
        "crafting_templates.xslt": '<x>\n\t<UsablePiece piece_id="wm_cave_troll_2h_mace_handle"/>\n</x>\n',
        "LOTRLOME_items/LOTRAOM_weapons.xml":
            '<Items>\r\n  <CraftedItem id="wm_cave_troll_2h_mace_a" name="{=aom_z}Z">\r\n'
            '    <Pieces>\r\n      <Piece id="wm_cave_troll_2h_mace_head" Type="Blade" />\r\n'
            '      <Piece id="wm_cave_troll_2h_mace_handle" Type="Handle" />\r\n    </Pieces>\r\n'
            '  </CraftedItem>\r\n</Items>\r\n',
        "Languages/loc_LOTRAOM_weapons.xml":
            '<base>\r\n  <strings>\r\n    <string id="aom_wm_cave_troll_2h_mace_a_name" text="Mace"/>\r\n'
            '  </strings>\r\n</base>\r\n',
        "Languages/loc_LOTRLOME_crafting_pieces.xml":
            '<base>\n  <strings>\n    <string id="aom_wm_cave_troll_2h_mace_handle_name" text="Handle"/>\n'
            '  </strings>\n</base>\n',
    }

    def test_apply_once_then_nothing_and_the_bytes_stay_faithful(self):
        import tempfile
        from unittest import mock
        with tempfile.TemporaryDirectory() as md:
            for rel, text in self.FILES.items():
                path = os.path.join(md, *rel.split("/"))
                os.makedirs(os.path.dirname(path), exist_ok=True)
                bom = b"\xef\xbb\xbf" if rel == "weapon_descriptions.xslt" else b""
                open(path, "wb").write(bom + text.encode("utf-8"))
            with mock.patch.object(h, "MD", md), mock.patch.object(h, "package_gaps", return_value=[]), \
                    mock.patch.object(h, "game_or_kit_running", return_value=False), \
                    mock.patch.object(h.os.path, "isfile", return_value=True):
                self.assertEqual(h.main(["--apply"]), 0)
                self.assertTrue(all(new is None for _p, _r, new, _n in h.plan()), "a second run must change nothing")
            wd = open(os.path.join(md, "weapon_descriptions.xslt"), "rb").read()
            self.assertTrue(wd.startswith(b"\xef\xbb\xbf"))
            for rel in self.FILES:
                b = open(os.path.join(md, *rel.split("/")), "rb").read()
                self.assertFalse(b.startswith(b"\xef\xbb\xbf") and rel != "weapon_descriptions.xslt", rel)
                self.assertNotIn(b"\r\r\n", b, rel)
                crlf = b"\r\n" in self.FILES[rel].encode()
                self.assertEqual(len(re.findall(rb"(?<!\r)\n", b)) == 0, crlf, rel)
            pieces = open(os.path.join(md, "LOTRLOME_crafting_pieces.xml"), "rb").read().decode()
            self.assertIn('id="wm_hill_troll_2h_hammer_head"', pieces)
            self.assertEqual(pieces.count('weight="0.875"'), 1)


if __name__ == "__main__":
    unittest.main()
