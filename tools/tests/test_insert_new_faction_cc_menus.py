#!/usr/bin/env python3
"""Tests for tools/insert_new_faction_cc_menus.py: the shipped menus, and a dry run and an apply on a
temp copy.

Run:  python -m pytest tools/tests/test_insert_new_faction_cc_menus.py -q

The ways this data goes wrong while every other gate stays green: a new culture missing from one of
the four culture-scoped menus (that CC stage renders blank and vanilla throws when the player
advances), a clone whose skills or attribute drifted from its source entry, authored text that names
the source culture's places, a narrative key nobody registered (English in all eleven other
languages), and a dry run that writes.
"""
import contextlib
import io
import json
import os
import re
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import insert_new_faction_cc_menus as cc  # noqa: E402

# Everything else on an entry is mechanics, and a clone takes it from its source unchanged.
NON_MECHANICS = {"string_id", "culture_id", "text", "description"}

# Erebor's own places and people, from the lore rather than from the tool's FORBIDDEN_FOR table, so
# this check does not share the assumption it audits (ClonedCultureFlavorTests' lesson).
EREBOR_IDENTITY = ("Erebor", "Lonely Mountain", "Dale", "Esgaroth", "Lake-town", "Smaug",
                   "Iron Hills", "Dain", "Dáin", "Ironfoot", "Ravenhill", "Arkenstone",
                   "Under the Mountain")
# At least one of these must appear in each Ered Luin menu, so the check above cannot pass on text
# that names no place at all.
ERED_LUIN_PLACES = ("Thorin's Halls", "Belegost", "Nogrod", "Blue Mountains", "Ered Luin",
                    "Mount Dolmed", "Dwarf-road", "Blue Craig", "Lindon")

# Dale's own places and people for Anduin Vale and Angaladh, from the lore rather than from the tool's
# FORBIDDEN_FOR table.
DALE_IDENTITY = ("Dale", "Bard", "Esgaroth", "Lake-town", "Lonely Mountain", "Girion", "Erebor",
                 "King Brand", "Long Lake", "Smaug")
# At least one of these must appear in each menu of its realm, so the check above cannot pass on text
# that names no place at all.
DALE_CLONE_PLACES = {
    "anduinvale": ("Beorn's Hall", "Old Ford", "Carrock", "Grimbeorn", "Gladden", "High Pass",
                   "Eastbank", "Misty Mountains", "Mirkwood"),
    "angaladh": ("Woodmen-town", "Ironwood Hall", "Forest Gate", "Thornhold", "Emyn Duir",
                 "Forest Road", "Ingomar", "Mirkwood"),
    "dorwinion": ("Vineholm", "Redwater", "Seagate", "Sunwall", "Vinewatch", "Eastmarch", "Ostric",
                  "Sea of Rhûn", "River Running"),
}

# Rohan's own places and people for South Rhovanion (culture id `vlandia`), from the lore rather
# than from the tool's FORBIDDEN_FOR table.
ROHAN_IDENTITY = ("Rohan", "Rohirrim", "Edoras", "Meduseld", "Théoden", "Éomer", "Éowyn",
                  "Helm's Deep", "Westfold", "Eastfold", "Eorl", "Riddermark", "Aldburg", "Isengard",
                  "Saruman", "Dunlending", "Wold")
SOUTH_RHOVANION_PLACES = ("Vidugard", "Horsemead", "Southwood", "Westmark", "Wainwatch", "Northmark",
                          "Vidumar", "Mirkwood", "Wainrider")
NEW_REALMS = ("eredluin", "anduinvale", "angaladh", "dorwinion", "southrhovanion")

# The shipped files, captured before any test patches cc.CC or cc.CC_STRINGS.
SHIPPED_CC = cc.CC
SHIPPED_STRINGS = cc.CC_STRINGS


def write_menu(dst_dir, menu, drop=()):
    """Copy a shipped menu into dst_dir without the entries of the cultures in `drop`."""
    raw = open(os.path.join(SHIPPED_CC, menu + "_menu.json"), encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in raw else "\n"
    kept = [e for e in json.loads(raw) if e.get("culture_id") not in drop]
    text = "[" + nl + ("," + nl).join(cc.fmt_entry(e, nl) for e in kept) + nl + "]" + nl
    with open(os.path.join(dst_dir, menu + "_menu.json"), "w", encoding="utf-8", newline="") as f:
        f.write(text)


def load(menu, cc_dir=None):
    with open(os.path.join(cc_dir or cc.CC, menu + "_menu.json"), encoding="utf-8") as f:
        return json.load(f)


def registered(path=None):
    root = ET.parse(path or cc.CC_STRINGS).getroot()
    return {e.get("id"): e.get("text") for e in root.iter("string")}


def run(argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        rc = cc.main(argv)
    return rc, out.getvalue()


class ShippedMenus(unittest.TestCase):
    def test_every_new_culture_clones_its_source_mechanics_entry_for_entry(self):
        for menu in cc.MENUS:
            data = load(menu)
            for culture in cc.NEW:
                src = [e for e in data if e.get("culture_id") == cc.SRC_FOR[culture]]
                new = [e for e in data if e.get("culture_id") == culture]
                with self.subTest(menu=menu, culture=culture):
                    self.assertTrue(new, "no entries: this CC stage renders blank")
                    self.assertEqual(len(new), len(src))
                    for s, n in zip(src, new):
                        self.assertEqual({k: v for k, v in s.items() if k not in NON_MECHANICS},
                                         {k: v for k, v in n.items() if k not in NON_MECHANICS})
                        self.assertEqual(n["string_id"],
                                         s["string_id"].replace(cc.SRC_FOR[culture], culture))

    def test_authored_text_count_matches_the_source_entry_count(self):
        for culture, menus in cc.AUTHORED.items():
            for menu in cc.MENUS:
                src = [e for e in load(menu) if e.get("culture_id") == cc.SRC_FOR[culture]]
                with self.subTest(culture=culture, menu=menu):
                    self.assertEqual(len(menus[menu]), len(src))

    def test_no_new_culture_names_its_source_culture(self):
        for menu in cc.MENUS:
            for e in load(menu):
                culture = e.get("culture_id")
                if culture not in cc.NEW:
                    continue
                for word in cc.FORBIDDEN_FOR[cc.SRC_FOR[culture]]:
                    with self.subTest(menu=menu, id=e["string_id"], word=word):
                        self.assertNotIn(word, e["text"] + " " + e["description"])

    def test_authored_text_has_no_em_or_en_dash(self):
        for culture, menus in cc.AUTHORED.items():
            for menu, rows in menus.items():
                for text, desc in rows:
                    with self.subTest(culture=culture, menu=menu, text=text):
                        self.assertNotIn("\u2014", text + desc)
                        self.assertNotIn("\u2013", text + desc)

    def test_every_new_culture_entry_is_registered_with_its_own_text(self):
        rows = registered()
        for menu in cc.MENUS:
            for e in load(menu):
                if e.get("culture_id") not in cc.NEW:
                    continue
                for suffix, value in (("text", e["text"]), ("desc", e["description"])):
                    key = f"taom_cc_{e['string_id']}_{suffix}"
                    with self.subTest(key=key):
                        self.assertEqual(rows.get(key), "{=" + key + "}" + value)

    def test_check_reports_nothing_pending_on_the_shipped_data(self):
        rc, out = run(["--check"])
        self.assertEqual(rc, 0, out)


class EredLuinText(unittest.TestCase):
    def test_ered_luin_is_cloned_from_erebor(self):
        self.assertIn("eredluin", cc.NEW)
        self.assertEqual(cc.SRC_FOR["eredluin"], "erebor")
        self.assertIn("eredluin", cc.AUTHORED)

    def test_ered_luin_text_names_no_erebor_place_or_person(self):
        for menu, rows in cc.AUTHORED["eredluin"].items():
            for text, desc in rows:
                for word in EREBOR_IDENTITY:
                    with self.subTest(menu=menu, text=text, word=word):
                        self.assertNotIn(word, text + " " + desc)

    def test_each_ered_luin_menu_names_the_blue_mountains(self):
        for menu, rows in cc.AUTHORED["eredluin"].items():
            blob = " ".join(t + " " + d for t, d in rows)
            with self.subTest(menu=menu):
                self.assertTrue(any(p in blob for p in ERED_LUIN_PLACES))


class DaleCloneText(unittest.TestCase):
    """Anduin Vale and Angaladh are cut from Dale (culture id `sturgia`) and take its mechanics."""

    def test_both_realms_clone_dale(self):
        for culture in DALE_CLONE_PLACES:
            with self.subTest(culture=culture):
                self.assertIn(culture, cc.NEW)
                self.assertEqual(cc.SRC_FOR[culture], "sturgia")
                self.assertIn(culture, cc.AUTHORED)

    def test_text_names_no_dale_place_or_person(self):
        for culture in DALE_CLONE_PLACES:
            for menu, rows in cc.AUTHORED[culture].items():
                for text, desc in rows:
                    for word in DALE_IDENTITY:
                        with self.subTest(culture=culture, menu=menu, text=text, word=word):
                            self.assertNotIn(word, text + " " + desc)

    def test_each_menu_names_the_realms_own_places(self):
        for culture, places in DALE_CLONE_PLACES.items():
            for menu, rows in cc.AUTHORED[culture].items():
                blob = " ".join(t + " " + d for t, d in rows)
                with self.subTest(culture=culture, menu=menu):
                    self.assertTrue(any(p in blob for p in places))

    def test_text_has_no_em_or_en_dash_and_six_entries_per_menu(self):
        for culture in DALE_CLONE_PLACES:
            for menu, rows in cc.AUTHORED[culture].items():
                with self.subTest(culture=culture, menu=menu):
                    self.assertEqual(len(rows), 6)
                    for text, desc in rows:
                        self.assertNotIn("\u2014", text + desc)
                        self.assertNotIn("\u2013", text + desc)

    def test_the_shipped_entries_keep_dales_mechanics_entry_for_entry(self):
        for menu in cc.MENUS:
            data = load(menu, SHIPPED_CC)
            dale = [e for e in data if e.get("culture_id") == "sturgia"]
            for culture in DALE_CLONE_PLACES:
                new = [e for e in data if e.get("culture_id") == culture]
                with self.subTest(menu=menu, culture=culture):
                    self.assertEqual(len(new), len(dale))
                    for a, b in zip(dale, new):
                        self.assertEqual({k: v for k, v in a.items() if k not in NON_MECHANICS},
                                         {k: v for k, v in b.items() if k not in NON_MECHANICS})


class RohanCloneText(unittest.TestCase):
    """South Rhovanion is cut from Rohan (culture id `vlandia`) and takes its mechanics."""

    def test_the_realm_clones_rohan(self):
        self.assertIn("southrhovanion", cc.NEW)
        self.assertEqual(cc.SRC_FOR["southrhovanion"], "vlandia")
        self.assertIn("southrhovanion", cc.AUTHORED)

    def test_the_forbidden_table_lists_rohans_words(self):
        for word in ROHAN_IDENTITY:
            if word in ("Saruman", "Dunlending", "Wold"):
                continue
            with self.subTest(word=word):
                self.assertIn(word, cc.FORBIDDEN_FOR["vlandia"])

    def test_text_names_no_rohan_place_or_person(self):
        for menu, rows in cc.AUTHORED["southrhovanion"].items():
            for text, desc in rows:
                for word in ROHAN_IDENTITY:
                    with self.subTest(menu=menu, text=text, word=word):
                        self.assertNotIn(word, text + " " + desc)

    def test_each_menu_names_the_realms_own_places(self):
        for menu, rows in cc.AUTHORED["southrhovanion"].items():
            blob = " ".join(t + " " + d for t, d in rows)
            with self.subTest(menu=menu):
                self.assertTrue(any(p in blob for p in SOUTH_RHOVANION_PLACES))

    def test_text_has_no_em_or_en_dash_and_six_entries_per_menu(self):
        for menu, rows in cc.AUTHORED["southrhovanion"].items():
            with self.subTest(menu=menu):
                self.assertEqual(len(rows), 6)
                for text, desc in rows:
                    self.assertNotIn("\u2014", text + desc)
                    self.assertNotIn("\u2013", text + desc)

    def test_the_shipped_entries_keep_rohans_mechanics_entry_for_entry(self):
        for menu in cc.MENUS:
            data = load(menu, SHIPPED_CC)
            rohan = [e for e in data if e.get("culture_id") == "vlandia"]
            new = [e for e in data if e.get("culture_id") == "southrhovanion"]
            with self.subTest(menu=menu):
                self.assertEqual(len(new), len(rohan))
                for a, b in zip(rohan, new):
                    self.assertEqual({k: v for k, v in a.items() if k not in NON_MECHANICS},
                                     {k: v for k, v in b.items() if k not in NON_MECHANICS})
                    self.assertEqual(b["string_id"], a["string_id"].replace("vlandia", "southrhovanion"))


class DryRunAndApply(unittest.TestCase):
    """The tool on a temp copy that lacks Ered Luin: a dry run writes nothing, --check fails, and
    --apply adds Erebor's count of entries in every menu and registers their strings. A clone
    source with no entries fails every mode before anything is written."""

    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="cc_menus_")
        self.cc_dir = os.path.join(self.tmp, "charactercreation")
        os.makedirs(self.cc_dir)
        for menu in cc.MENUS:
            write_menu(self.cc_dir, menu, drop=set(NEW_REALMS))
        raw = open(cc.CC_STRINGS, "rb").read()
        bom = raw.startswith(b"\xef\xbb\xbf")
        text = re.sub(r'[^\n]*id="taom_cc_taom_[a-z]+_(?:eredluin|anduinvale|angaladh|dorwinion|southrhovanion)_\d+_(?:text|desc)"[^\n]*\n', "",
                      raw.decode("utf-8-sig"))
        self.strings = os.path.join(self.tmp, "taom_cc_strings.xml")
        open(self.strings, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
        patches = [mock.patch.object(cc, "CC", self.cc_dir),
                   mock.patch.object(cc, "CC_STRINGS", self.strings)]
        for p in patches:
            p.start()
            self.addCleanup(p.stop)
        self.addCleanup(shutil.rmtree, self.tmp, True)

    def snapshot(self):
        files = [os.path.join(self.cc_dir, m + "_menu.json") for m in cc.MENUS] + [self.strings]
        return {f: open(f, "rb").read() for f in files}

    def test_dry_run_writes_nothing_and_check_fails(self):
        before = self.snapshot()
        rc, out = run([])
        self.assertEqual(rc, 0, out)
        self.assertIn("DRY RUN", out)
        self.assertEqual(before, self.snapshot())
        rc, out = run(["--check"])
        self.assertEqual(rc, 1, out)
        self.assertEqual(before, self.snapshot())

    def test_apply_writes_nothing_when_a_later_output_fails(self):
        # tools/README.md "XML I/O convention": every output is prepared and parsed before the
        # first write, so a strings file that cannot be built leaves the four menus untouched too.
        before = self.snapshot()
        with mock.patch.object(cc, "prepare_cc_strings", side_effect=RuntimeError("strings broke")):
            with self.assertRaises(RuntimeError):
                run(["--apply"])
        self.assertEqual(before, self.snapshot(), "a menu was written before every output was ready")

    def test_apply_adds_ered_luin_everywhere_and_is_idempotent(self):
        before = {m: load(m, self.cc_dir) for m in cc.MENUS}
        rc, out = run(["--apply"])
        self.assertEqual(rc, 0, out)
        for menu in cc.MENUS:
            after = load(menu, self.cc_dir)
            erebor = [e for e in after if e.get("culture_id") == "erebor"]
            new = [e for e in after if e.get("culture_id") == "eredluin"]
            with self.subTest(menu=menu):
                self.assertEqual(len(new), len(erebor))
                self.assertEqual(after[:len(before[menu])], before[menu],
                                 "an existing entry changed")
        rows = registered(self.strings)
        for menu in cc.MENUS:
            for e in load(menu, self.cc_dir):
                if e.get("culture_id") == "eredluin":
                    self.assertIn(f"taom_cc_{e['string_id']}_text", rows)
                    self.assertIn(f"taom_cc_{e['string_id']}_desc", rows)
        once = self.snapshot()
        rc, out = run(["--check"])
        self.assertEqual(rc, 0, out)
        run(["--apply"])
        self.assertEqual(once, self.snapshot(), "a second --apply changed the files")

    def test_apply_adds_dorwinion_and_south_rhovanion_with_their_sources_entry_count_and_registers_them(self):
        run(["--apply"])
        for menu in cc.MENUS:
            after = load(menu, self.cc_dir)
            for culture, source in (("dorwinion", "sturgia"), ("southrhovanion", "vlandia")):
                src = [e for e in after if e.get("culture_id") == source]
                new = [e for e in after if e.get("culture_id") == culture]
                with self.subTest(menu=menu, culture=culture):
                    self.assertEqual(len(new), len(src))
                    self.assertTrue(all(culture in e["string_id"] and source not in e["string_id"]
                                        for e in new))
        rows = registered(self.strings)
        for culture in ("dorwinion", "southrhovanion"):
            for menu in cc.MENUS:
                for e in load(menu, self.cc_dir):
                    if e.get("culture_id") == culture:
                        self.assertIn(f"taom_cc_{e['string_id']}_text", rows)
                        self.assertIn(f"taom_cc_{e['string_id']}_desc", rows)

    def test_apply_adds_anduin_vale_and_angaladh_with_dales_entry_count_and_registers_them(self):
        run(["--apply"])
        for menu in cc.MENUS:
            after = load(menu, self.cc_dir)
            dale = [e for e in after if e.get("culture_id") == "sturgia"]
            for culture in ("anduinvale", "angaladh"):
                new = [e for e in after if e.get("culture_id") == culture]
                with self.subTest(menu=menu, culture=culture):
                    self.assertEqual(len(new), len(dale))
                    self.assertTrue(all(culture in e["string_id"] and "sturgia" not in e["string_id"] for e in new))
        rows = registered(self.strings)
        for culture in ("anduinvale", "angaladh"):
            for menu in cc.MENUS:
                for e in load(menu, self.cc_dir):
                    if e.get("culture_id") == culture:
                        self.assertIn(f"taom_cc_{e['string_id']}_text", rows)
                        self.assertIn(f"taom_cc_{e['string_id']}_desc", rows)

    def test_a_clone_source_with_no_entries_fails_every_mode_and_writes_nothing(self):
        # The shipped files, except that youth has neither Erebor's entries nor Ered Luin's. Ered
        # Luin clones Erebor, so there is nothing to clone and its youth stage would render blank;
        # a warning there let --check exit 0 and --apply add nothing.
        for menu in cc.MENUS:
            shutil.copyfile(os.path.join(SHIPPED_CC, menu + "_menu.json"),
                            os.path.join(self.cc_dir, menu + "_menu.json"))
        shutil.copyfile(SHIPPED_STRINGS, self.strings)
        write_menu(self.cc_dir, "youth", drop={"erebor", "eredluin"})
        before = self.snapshot()
        for argv in (["--check"], ["--apply"], []):
            with self.subTest(argv=argv):
                with self.assertRaises(SystemExit) as raised:
                    run(argv)
                self.assertIn("youth/eredluin", str(raised.exception))
                self.assertEqual(before, self.snapshot())


if __name__ == "__main__":
    unittest.main()
