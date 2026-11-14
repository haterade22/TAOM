"""Banner icon sheets: the packing tool and the shipped-sheet gate (#1047).

Run:  python -m pytest tools/tests/test_pack_banner_sheets.py -q

The tool is exercised on small temporary module trees (SHEET_PX patched to 64, so cells are 16 px):
one red test per audit finding kind, the pack and prune paths, and one test that the repo's own sheets
audit clean.
"""
import sys
import tempfile
import unittest
import uuid
from pathlib import Path
from unittest import mock

REPO = Path(__file__).resolve().parent.parent.parent
TOOLS = REPO / "tools"
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))

try:
    import numpy as np
    import xxhash
    from PIL import Image
except ImportError as exc:  # the house guard (test_build_loading_ring_sheet.py)
    raise unittest.SkipTest(f"pack_banner_sheets needs numpy, Pillow and xxhash ({exc})")

import pack_banner_sheets as pbs  # noqa: E402

PX = 64
C = PX // 4


def sheet_png(path, art_cells):
    """A PX sheet: a green mark in each art cell, the placeholder checkerboard elsewhere."""
    slots = []
    for i in range(16):
        if i in art_cells:
            cell = np.zeros((C, C, 3), dtype=np.uint8)
            cell[4:12, 4:12, 1] = 200 + (i % 50)  # 64 pixels, distinct per cell
            slots.append(cell)
        else:
            slots.append(None)
    with mock.patch.object(pbs, "SHEET_PX", PX):
        Image.fromarray(pbs.assemble(slots), "RGB").convert("RGBA").save(path)


def drop_alpha(path):
    """Re-save a sheet as 3-channel RGB, the source the Kit compiles to DXT1."""
    Image.open(path).convert("RGB").save(path, format="PNG")


def packages(root, mat, source_name=None):
    """_tex/_mtl.tpac shaped like the Kit's: package GUID at 8, texture GUID at 0x34, source path and hash."""
    src = root / pbs.SOURCES / (source_name or f"{mat}.png")
    tex_guid = uuid.uuid4().bytes_le
    path = f"$BASE/Modules/TAOM/AssetSources/BannerIcons/{src.name}".encode()
    digest = xxhash.xxh64(src.read_bytes()).intdigest().to_bytes(8, "little") if src.exists() else bytes(8)
    tex = b"TPAC" + bytes(4) + uuid.uuid4().bytes_le + bytes(0x34 - 24) + tex_guid + path + digest
    (root / pbs.PACKAGES / f"{mat}_tex.tpac").write_bytes(tex)
    (root / pbs.PACKAGES / f"{mat}_mtl.tpac").write_bytes(b"TPAC" + bytes(4) + uuid.uuid4().bytes_le + tex_guid)
    return tex


def write_module(root, rows, art=None, sprites=None, with_packages=True, extra_art=None):
    """A module tree: banner_icons.xml with rows [(id, material, index)], a sheet per material with art in
    each named cell (art overrides per material), sprites for every id (or `sprites`), both packages."""
    for d in (pbs.XML.parent, pbs.SOURCES, pbs.PACKAGES, pbs.SPRITE_PNGS, pbs.CACHE):
        (root / d).mkdir(parents=True, exist_ok=True)
    lines = "".join(f'      <Icon id="{i}" material_name="{m}" texture_index="{t}" />\r\n' for i, m, t in rows)
    (root / pbs.XML).write_bytes(f'<?xml version="1.0" encoding="utf-8"?>\r\n<base>\r\n    <BannerIconGroup id="1">\r\n'
                                 f'{lines}    </BannerIconGroup>\r\n</base>\r\n'.encode())
    mats = sorted({m for _, m, _ in rows})
    for m in mats:
        cells = set((art or {}).get(m, {t for _, mm, t in rows if mm == m})) | set((extra_art or {}).get(m, ()))
        sheet_png(root / pbs.SOURCES / f"{m}.png", cells)
        if with_packages:
            packages(root, m)
    ids = [i for i, _, _ in rows] if sprites is None else sprites
    parts = "".join(f"<SpritePart><Name>{i}</Name><CategoryName>{pbs.SPRITE_CATEGORY}</CategoryName></SpritePart>"
                    for i in ids)
    (root / pbs.SPRITE_DATA).write_text(f"<SpriteData><SpriteParts>{parts}</SpriteParts></SpriteData>")
    for i in ids:
        (root / pbs.SPRITE_PNGS / f"{i}.png").write_bytes(b"png")


FULL = [(str(100 + t), "taom_banners_01", t) for t in range(16)]
PACKED1 = [(str(200 + t), "taom_banners_02", t) for t in range(12)]   # 4 free cells


class TreeTest(unittest.TestCase):
    def setUp(self):
        p = mock.patch.object(pbs, "SHEET_PX", PX)
        p.start()
        self.addCleanup(p.stop)
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name) / "repo"
        self.install = Path(tmp.name) / "install"

    def findings(self, root=None):
        return pbs.findings(pbs.audit(root or self.root))


class AuditFindsEachKindTests(TreeTest):
    def test_the_clean_fixture_has_no_findings(self):
        write_module(self.root, FULL + PACKED1)
        self.assertEqual(self.findings(), {})

    def test_an_icon_naming_a_missing_sheet(self):
        write_module(self.root, FULL + PACKED1)
        (self.root / pbs.SOURCES / "taom_banners_02.png").unlink()
        self.assertIn("missing_sheet", self.findings())

    def test_an_icon_on_a_placeholder_cell(self):
        write_module(self.root, FULL + PACKED1, art={"taom_banners_02": set(range(11))})
        self.assertEqual(set(self.findings()), {"empty_cell"})

    def test_two_icons_on_one_cell(self):
        write_module(self.root, FULL + PACKED1 + [("299", "taom_banners_02", 3)])
        self.assertIn("shared_cell", self.findings())

    def test_art_in_a_cell_no_icon_names(self):
        write_module(self.root, FULL + PACKED1, extra_art={"taom_banners_02": {14}})
        self.assertEqual(set(self.findings()), {"unnamed_art"})

    def test_an_icon_missing_from_the_sprite_data(self):
        write_module(self.root, FULL + PACKED1, sprites=[i for i, _, _ in FULL + PACKED1][:-1])
        (self.root / pbs.SPRITE_PNGS / "211.png").write_bytes(b"png")  # the PNG alone is not enough
        self.assertEqual(self.findings(), {"missing_sprite": ["211"]})

    def test_an_icon_missing_its_sprite_png(self):
        write_module(self.root, FULL + PACKED1)
        (self.root / pbs.SPRITE_PNGS / "211.png").unlink()
        self.assertEqual(self.findings(), {"missing_sprite": ["211"]})

    def test_a_texture_imported_from_another_source_file(self):
        write_module(self.root, FULL + PACKED1)
        packages(self.root, "taom_banners_01", source_name="taom_banners_01.psd")
        self.assertEqual(set(self.findings()), {"stale_package"})

    def test_a_sheet_source_without_an_alpha_channel(self):
        write_module(self.root, FULL + PACKED1)
        drop_alpha(self.root / pbs.SOURCES / "taom_banners_02.png")
        packages(self.root, "taom_banners_02")  # fresh hash, so only the channel finding remains
        self.assertEqual(self.findings(), {"no_alpha": ["taom_banners_02"]})

    def test_a_psd_source_without_an_alpha_channel(self):
        write_module(self.root, FULL + PACKED1)
        src = self.root / pbs.SOURCES / "taom_banners_02.png"
        drop_alpha(src)
        src.rename(src.with_suffix(".psd"))
        packages(self.root, "taom_banners_02", "taom_banners_02.psd")
        self.assertEqual(self.findings(), {"no_alpha": ["taom_banners_02"]})

    def test_two_rows_with_one_id(self):
        write_module(self.root, FULL + PACKED1 + [("200", "taom_banners_02", 12)])
        self.assertIn("duplicate_id", self.findings())

    def test_a_material_without_its_packages(self):
        write_module(self.root, FULL + PACKED1)
        (self.root / pbs.PACKAGES / "taom_banners_01_mtl.tpac").unlink()
        self.assertEqual(set(self.findings()), {"missing_package"})

    def test_a_source_changed_after_the_kit_import(self):
        write_module(self.root, FULL + PACKED1)
        sheet_png(self.root / pbs.SOURCES / "taom_banners_02.png", set(range(12)) | {12})
        rep = self.findings()
        self.assertIn("stale_package", rep)

    def test_a_material_that_names_another_texture(self):
        write_module(self.root, FULL + PACKED1)
        mtl = self.root / pbs.PACKAGES / "taom_banners_01_mtl.tpac"
        mtl.write_bytes(mtl.read_bytes()[:-16] + uuid.uuid4().bytes_le)
        self.assertEqual(set(self.findings()), {"stale_package"})

    def test_a_sheet_no_icon_names(self):
        write_module(self.root, FULL + PACKED1)
        sheet_png(self.root / pbs.SOURCES / "taom_banners_old_alpha_01.png", {0})
        self.assertEqual(set(self.findings()), {"unused_sheet"})

    def test_a_partly_used_sheet_that_fits_free_cells_of_another_is_mergeable(self):
        write_module(self.root, FULL + PACKED1 + [("300", "taom_banners_03", 0)])
        self.assertEqual(self.findings(), {"mergeable": ["taom_banners_03"]})

    def test_sheets_that_cannot_merge_are_not_a_finding(self):
        rows = [(str(400 + 16 * s + t), f"taom_banners_{s + 3:02d}", t) for s in range(3) for t in range(9)]
        write_module(self.root, FULL + rows)
        self.assertNotIn("mergeable", self.findings())

    def test_a_sheet_not_named_taom_banners_nn_has_a_legacy_name(self):
        write_module(self.root, FULL + [(str(300 + t), "taom_banners_gondor_alpha_01", t) for t in range(16)])
        self.assertEqual(self.findings(), {"legacy_name": ["taom_banners_gondor_alpha_01"]})

    def test_a_file_with_no_icon_rows_stops_the_audit(self):
        write_module(self.root, FULL)
        (self.root / pbs.XML).write_text("<base><BannerIconGroup id='1'/></base>")
        with self.assertRaises(ValueError):
            pbs.audit(self.root)


class PlanTests(unittest.TestCase):
    @staticmethod
    def icons(counts):
        return [(f"{m}{t}", m, t) for m, n in counts.items() for t in range(n)]

    def test_full_and_well_named_sheets_never_move(self):
        moves = pbs.plan(self.icons({"taom_banners_01": 16, "taom_banners_02": 10, "taom_banners_03": 3}))
        self.assertEqual({m for m, _, _, _ in moves.values()}, {"taom_banners_03"})

    def test_a_small_sheet_fills_free_cells_of_a_bigger_one(self):
        moves = pbs.plan(self.icons({"taom_banners_02": 10, "taom_banners_05": 3}))
        self.assertEqual(sorted((s, c) for _, _, s, c in moves.values()),
                         [("taom_banners_02", 10), ("taom_banners_02", 11), ("taom_banners_02", 12)])

    def test_legacy_names_are_numbered_in_order_of_first_appearance(self):
        moves = pbs.plan(self.icons({"zeta": 16, "alpha": 16}))
        self.assertEqual({m: s for m, _, s, _ in moves.values()}, {"zeta": "taom_banners_01", "alpha": "taom_banners_02"})
        self.assertTrue(all(t == c for _, t, _, c in moves.values()))

    def test_a_new_name_skips_every_taken_name(self):
        moves = pbs.plan(self.icons({"a": 5, "b": 4}), taken={"Taom_Banners_01", "taom_banners_03"})
        self.assertEqual({s for _, _, s, _ in moves.values()}, {"taom_banners_02"})

    def test_a_well_named_sheet_keeps_its_name_and_others_number_around_it(self):
        moves = pbs.plan(self.icons({"taom_banners_01": 16, "a": 16}))
        self.assertEqual({s for _, _, s, _ in moves.values()}, {"taom_banners_02"})

    def test_a_non_png_sheet_that_receives_cells_is_written_under_a_new_name(self):
        moves = pbs.plan(self.icons({"taom_banners_01": 9, "taom_banners_02": 5}), non_png={"TAOM_banners_01"})
        self.assertEqual({m for m, _, _, _ in moves.values()}, {"taom_banners_01", "taom_banners_02"})
        self.assertEqual({s for _, _, s, _ in moves.values()}, {"taom_banners_03"})

    def test_a_non_png_sheet_that_receives_nothing_stays(self):
        self.assertEqual(pbs.plan(self.icons({"taom_banners_01": 16}), non_png={"taom_banners_01"}), {})

    def test_a_plan_that_saves_no_sheet_and_renames_nothing_is_empty(self):
        self.assertEqual(pbs.plan(self.icons({"taom_banners_01": 10, "taom_banners_02": 9})), {})

    def test_a_sheet_that_fits_nowhere_stays_put(self):
        moves = pbs.plan(self.icons({"taom_banners_02": 13, "taom_banners_04": 10,
                                     "taom_banners_05": 9, "taom_banners_06": 3}))
        self.assertEqual({m for m, _, _, _ in moves.values()}, {"taom_banners_06"})

    def test_packing_the_packed_tree_again_moves_nothing(self):
        counts = {f"taom_banners_{n:02d}": c for n, c in enumerate([16, 15, 16, 15, 15], 1)}
        self.assertEqual(pbs.plan(self.icons(counts)), {})

    def test_the_merges_are_the_same_whatever_the_input_order(self):
        icons = self.icons({"x": 7, "y": 7, "z": 2, "w": 15, "v": 1})
        self.assertEqual(pbs.merges(icons), pbs.merges(list(reversed(icons))))

    def test_the_twenty_shipped_partial_sheets_packed_into_ten(self):
        counts = dict(zip("abcdefghijklmnopqrst", [7, 7, 2, 1, 4, 11, 15, 11, 15, 7, 4, 5, 4, 6, 10, 4, 11, 9, 10, 7]))
        moved = {m for m, _, _, _ in pbs.merges(self.icons(counts)).values()}
        self.assertEqual(len(set(counts) - moved), 10)

    def test_a_pure_rename_is_a_copy_and_a_merge_is_not(self):
        icons = self.icons({"taom_banners_01": 9, "x": 16, "y": 5})
        moves = pbs.plan(icons)
        self.assertEqual(pbs.renames(icons, moves), {"taom_banners_02": "x"})

    def test_a_kept_sheet_receiving_cells_at_matching_indices_is_not_a_copy(self):
        icons = [(f"a{t}", "taom_banners_01", t) for t in range(9)] + [("b10", "b", 10)]
        moves = pbs.plan(icons)
        self.assertEqual({s for _, _, s, _ in moves.values()}, {"taom_banners_01"})
        self.assertEqual(pbs.renames(icons, moves), {})


class RewriteTests(unittest.TestCase):
    XML = '<base>\r\n      <Icon id="100" material_name="a" texture_index="0" />\r\n</base>\r\n'

    def test_rewrite_changes_only_the_moved_row_and_keeps_crlf(self):
        out = pbs.rewrite_icons(self.XML, {"100": ("a", 0, "p", 7)})
        self.assertEqual(out, self.XML.replace('"a" texture_index="0"', '"p" texture_index="7"'))

    def test_rewrite_refuses_a_row_it_cannot_match(self):
        with self.assertRaises(ValueError):
            pbs.rewrite_icons(self.XML.replace('id="100" material_name', 'material_name="a" id="100" x'),
                              {"100": ("a", 0, "p", 7)})


class SheetTests(unittest.TestCase):
    def test_free_slots_get_the_red_green_checkerboard(self):
        with mock.patch.object(pbs, "SHEET_PX", PX):
            sheet = pbs.assemble([None] * 16)
        self.assertEqual(tuple(sheet[0, 0]), (255, 0, 0))
        self.assertEqual(tuple(sheet[0, C]), (0, 255, 0))
        self.assertEqual(tuple(sheet[C, 0]), (0, 255, 0))

    def test_a_placeholder_is_not_art_and_a_small_mark_is(self):
        self.assertFalse(pbs.is_art(np.full((32, 32, 3), (255, 0, 0), dtype=np.uint8)))
        cell = np.full((64, 64, 3), (0, 255, 0), dtype=np.uint8)
        cell[0, :8] = (1, 254, 0)  # Rohan cell 11's stray pixels
        self.assertFalse(pbs.is_art(cell))
        mark = np.zeros((64, 64, 3), dtype=np.uint8)
        mark[30:38, 30:38, 1] = 255
        self.assertTrue(pbs.is_art(mark))

    def test_fewer_than_32_off_pixels_is_not_art(self):
        cell = np.zeros((64, 64, 3), dtype=np.uint8)
        cell[1, :31, 1] = 255  # row 1: row 0 col 0 is the reference colour
        self.assertFalse(pbs.is_art(cell))
        cell[1, 31, 1] = 255
        self.assertTrue(pbs.is_art(cell))


class PackTests(TreeTest):
    def test_pack_moves_a_small_sheet_into_free_packed_cells_with_its_art(self):
        write_module(self.root, FULL + PACKED1 + [("300", "taom_banners_03", 2)])
        before = np.asarray(Image.open(self.root / pbs.SOURCES / "taom_banners_03.png"))
        self.assertEqual(pbs.pack(self.root, None, write=True), 0)
        rows = {i: (m, t) for i, m, t in pbs.read_icons((self.root / pbs.XML).read_bytes())}
        self.assertEqual(rows["300"], ("taom_banners_02", 12))
        after = np.asarray(Image.open(self.root / pbs.SOURCES / "taom_banners_02.png"))
        self.assertTrue(np.array_equal(after[3 * C:4 * C, 0:C], before[0:C, 2 * C:3 * C]))
        self.assertTrue(np.array_equal(after[0:C, 0:C][4:12, 4:12, 1], np.full((8, 8), 200, np.uint8)))

    def test_a_second_pack_writes_nothing(self):
        write_module(self.root, FULL + PACKED1 + [("300", "taom_banners_03", 2)])
        pbs.pack(self.root, None, write=True)
        snapshot = {p: p.read_bytes() for p in (self.root / pbs.SOURCES).iterdir()} | {
            self.root / pbs.XML: (self.root / pbs.XML).read_bytes()}
        self.assertEqual(pbs.pack(self.root, None, write=True), 0)
        self.assertEqual({p: p.read_bytes() for p in snapshot}, snapshot)

    def test_pack_refuses_on_each_blocking_finding(self):
        new = [("300", "taom_banners_03", 2)]
        cases = {
            "missing_sheet": (FULL + PACKED1 + new + [("301", "taom_banners_09", 0)], None),
            "empty_cell": (FULL + PACKED1 + new, {"taom_banners_02": set(range(11))}),
            "shared_cell": (FULL + PACKED1 + new + [("302", "taom_banners_02", 3)], None),
            "duplicate_id": (FULL + PACKED1 + [("200", "taom_banners_03", 2)], None),
        }
        for kind, (rows, art) in cases.items():
            with self.subTest(kind):
                root = self.root.parent / kind
                write_module(root, rows, art=art)
                if kind == "missing_sheet":
                    (root / pbs.SOURCES / "taom_banners_09.png").unlink()
                xml = (root / pbs.XML).read_bytes()
                self.assertIn(kind, self.findings(root))
                self.assertEqual(pbs.pack(root, None, write=True), 1)
                self.assertEqual((root / pbs.XML).read_bytes(), xml)

    def test_pack_refuses_to_drop_unnamed_art(self):
        write_module(self.root, FULL + PACKED1 + [("300", "taom_banners_03", 2)],
                     extra_art={"taom_banners_03": {5}})
        xml = (self.root / pbs.XML).read_bytes()
        self.assertEqual(pbs.pack(self.root, None, write=True), 1)
        self.assertEqual((self.root / pbs.XML).read_bytes(), xml)

    def test_pack_writes_each_sheet_with_an_opaque_alpha_channel(self):
        write_module(self.root, FULL + PACKED1 + [("300", "taom_banners_03", 2)])
        self.assertEqual(pbs.pack(self.root, None, write=True), 0)
        with Image.open(self.root / pbs.SOURCES / "taom_banners_02.png") as img:
            self.assertEqual(img.mode, "RGBA")
            self.assertEqual(img.getchannel("A").getextrema(), (255, 255))
        self.assertNotIn("no_alpha", self.findings())  # the re-import and prune findings are expected

    def test_pack_writes_the_install_sources_too(self):
        write_module(self.root, FULL + PACKED1 + [("300", "taom_banners_03", 2)])
        (self.install / pbs.SOURCES).mkdir(parents=True)
        pbs.pack(self.root, self.install, write=True)
        self.assertTrue((self.install / pbs.SOURCES / "taom_banners_02.png").exists())

    def as_psd(self, mat):
        """The sheet's PNG renamed .psd (Pillow reads by content), standing in for an editor PSD."""
        src = self.root / pbs.SOURCES / f"{mat}.png"
        return src.rename(src.with_suffix(".psd"))

    def test_a_legacy_sheet_is_renamed_by_copying_its_source_bytes(self):
        legacy = [(str(300 + t), "taom_banners_gondor_alpha_01", t) for t in range(16)]
        write_module(self.root, legacy + PACKED1)
        data = self.as_psd("taom_banners_gondor_alpha_01").read_bytes()
        self.assertEqual(pbs.pack(self.root, None, write=True), 0)
        rows = {i: (m, t) for i, m, t in pbs.read_icons((self.root / pbs.XML).read_bytes())}
        self.assertEqual({rows[i] for i, _, _ in legacy}, {("taom_banners_01", t) for t in range(16)})
        self.assertEqual((self.root / pbs.SOURCES / "taom_banners_01.psd").read_bytes(), data)
        self.assertFalse((self.root / pbs.SOURCES / "taom_banners_01.png").exists())
        self.assertTrue((self.root / pbs.SOURCES / "taom_banners_gondor_alpha_01.psd").exists())  # prune deletes it

    def test_two_psd_sheets_merge_into_a_new_png(self):
        a = [(str(300 + t), "taom_banners_dwarves_alpha_02", t) for t in range(9)]
        b = [(str(400 + t), "taom_banners_elven_alpha_01", t) for t in range(5)]
        write_module(self.root, FULL + a + b)
        self.as_psd("taom_banners_dwarves_alpha_02")
        before = pbs.load_cells(self.as_psd("taom_banners_elven_alpha_01"))
        self.assertEqual(pbs.pack(self.root, None, write=True), 0)
        rows = {i: (m, t) for i, m, t in pbs.read_icons((self.root / pbs.XML).read_bytes())}
        self.assertEqual(rows["300"], ("taom_banners_02", 0))
        self.assertEqual(rows["404"], ("taom_banners_02", 13))
        after = pbs.load_cells(self.root / pbs.SOURCES / "taom_banners_02.png")
        self.assertTrue(np.array_equal(after[13], before[4]))
        self.assertEqual(set(self.findings()) & set(pbs.BLOCKING), set())


class PruneTests(TreeTest):
    def setUp(self):
        super().setUp()
        write_module(self.root, FULL + PACKED1)
        write_module(self.install, FULL + PACKED1)
        for r in (self.root, self.install):
            sheet_png(r / pbs.SOURCES / "taom_banners_old_alpha_01.png", {0})
        self.tex = packages(self.install, "taom_banners_old_alpha_01")
        packages(self.root, "taom_banners_old_alpha_01")
        guid = str(uuid.UUID(bytes_le=self.tex[8:24])).upper()
        self.rdc = self.install / pbs.CACHE / f"{guid}.rdc"
        self.rdc.write_bytes(b"cache")
        self.backup = self.root.parent / "backup"

    def old_files(self, r):
        return sorted(p.name for p in pbs.sheet_files(r) if "old" in p.name)

    def test_prune_dry_run_deletes_nothing(self):
        pbs.prune(self.root, self.install, None, write=False)
        self.assertEqual(len(self.old_files(self.root)), 3)
        self.assertTrue(self.rdc.exists())

    def test_prune_deletes_unnamed_sheets_in_both_trees_with_backups(self):
        self.assertEqual(pbs.prune(self.root, self.install, self.backup, write=True), 0)
        self.assertEqual(self.old_files(self.root) + self.old_files(self.install), [])
        self.assertFalse(self.rdc.exists())
        self.assertTrue((self.backup / "install" / pbs.CACHE / self.rdc.name).exists())
        self.assertTrue((self.backup / "repo" / pbs.SOURCES / "taom_banners_old_alpha_01.png").exists())

    def test_prune_lists_each_rdc_before_its_texture(self):
        out = []
        with mock.patch("builtins.print", side_effect=lambda *a, **k: out.append(" ".join(map(str, a)))):
            pbs.prune(self.root, self.install, None, write=False)
        install_lines = [line for line in out if str(self.install) in line]
        names = [Path(line.split(" ", 2)[-1]).name for line in install_lines]
        self.assertLess(names.index(self.rdc.name), names.index("taom_banners_old_alpha_01_tex.tpac"))

    def test_prune_keeps_sheets_the_deployed_xml_still_names(self):
        rows = FULL + PACKED1 + [("900", "taom_banners_old_alpha_01", 0)]
        (self.install / pbs.XML).write_bytes("".join(
            ['<base><BannerIconGroup id="1">']
            + [f'<Icon id="{i}" material_name="{m}" texture_index="{t}" />' for i, m, t in rows]
            + ["</BannerIconGroup></base>"]).encode())
        pbs.prune(self.root, self.install, self.backup, write=True)
        # one keep set for both trees: nothing goes until the build stops naming it
        self.assertEqual(len(self.old_files(self.install)), 3)
        self.assertEqual(len(self.old_files(self.root)), 3)

    def test_prune_keeps_a_named_sheet_written_in_another_case(self):
        src = self.root / pbs.SOURCES / "taom_banners_01.png"
        src.rename(src.with_name("TAOM_banners_01.png"))
        with mock.patch.object(pbs, "audit", return_value={"unused_sheet": ["x"]}):
            pbs.prune(self.root, self.install, self.backup, write=True)
        self.assertTrue((self.root / pbs.SOURCES / "TAOM_banners_01.png").exists())

    def test_prune_refuses_while_the_audit_has_other_findings(self):
        (self.root / pbs.PACKAGES / "taom_banners_01_mtl.tpac").unlink()
        self.assertEqual(pbs.prune(self.root, self.install, self.backup, write=True), 1)
        self.assertEqual(len(self.old_files(self.root)), 3)

    def test_main_refuses_to_write_while_the_game_runs(self):
        with mock.patch.object(pbs, "game_or_kit_running", return_value=True), \
             mock.patch.object(pbs, "game_modules", return_value=self.install.parent), \
             mock.patch.object(pbs, "REPO_MODULE", self.root):
            self.install.rename(self.install.parent / "TAOM")
            self.assertEqual(pbs.main(["--prune", "--apply", "--backup-dir", str(self.backup)]), 1)
        self.assertEqual(len(self.old_files(self.root)), 3)


class MainTests(TreeTest):
    def run_main(self, *argv):
        with mock.patch.object(pbs, "REPO_MODULE", self.root), \
             mock.patch.object(pbs, "game_modules", return_value=self.root.parent / "none"):
            return pbs.main(list(argv))

    def test_malformed_xml_cannot_run(self):
        write_module(self.root, FULL)
        (self.root / pbs.XML).write_text("<base><Icon id='1'")
        self.assertEqual(self.run_main("--audit"), 2)

    def test_a_row_without_texture_index_cannot_run(self):
        write_module(self.root, FULL)
        (self.root / pbs.XML).write_text("<base><Icon id='1' material_name='a' /></base>")
        self.assertEqual(self.run_main("--audit"), 2)

    def test_conflicting_flags_are_refused(self):
        write_module(self.root, FULL)
        for argv in (["--audit", "--apply"], ["--apply", "--backup-dir", "x"], ["--prune", "--apply"]):
            with self.subTest(argv), self.assertRaises(SystemExit):
                self.run_main(*argv)


class ShippedSheetGateTests(unittest.TestCase):
    def test_the_repo_sheets_audit_clean(self):
        self.assertEqual(pbs.findings(pbs.audit(pbs.REPO_MODULE)), {})


if __name__ == "__main__":
    unittest.main()
