"""Tests for tools/name_tag_inventory.py and tools/generate_name_tags.py (#1023).

Most run on synthetic XML and a synthetic rule table. ShippedTreeTests read the repo's ModuleData and the
tracked table (no game install needed).
"""

import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_name_tags as gen  # noqa: E402
import name_tag_inventory as inv  # noqa: E402
import name_tags as nt  # noqa: E402


def write(path: Path, content: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")
    return path


def spec(**over):
    base = {
        "plain": "{.NS}",
        "plain_by_category": {},
        "exclude": {},
        "allowed": ["{.NS}", "{.MS}", "{.FS}", "{.MP}", "{.FP}", "{.FN}", "{.MN}"],
        "head_nouns": {"Aldeia": "{.FS}", "Casa": "{.FS}", "Reino": "{.MS}", "Gobelins": "{.MP}", "Prole": "{.FS}"},
        "proper_prefixes": ["Barad", "Nos"],
        "english_heads": ["House", "Kingdom", "The"],
        "articles": ["O", "A", "Os", "As"],
        "pins": {},
        "tags": {},
    }
    base.update(over)
    return base


class DecideTests(unittest.TestCase):
    def test_decide_SingleWord_IsPlain(self):
        self.assertEqual(gen.decide("BR", "Erebor", "Erebor", spec())[0], "{.NS}")

    def test_decide_HeadNoun_GetsNounTag(self):
        self.assertEqual(gen.decide("BR", "Aldeia Norte", "Village North", spec()), ("{.FS}", "head noun Aldeia"))

    def test_decide_PluralCommonNoun_GetsPluralTag(self):
        self.assertEqual(gen.decide("BR", "Gobelins de Blue Craig", "Goblins of Blue Craig", spec())[0], "{.MP}")

    def test_decide_ProperPrefix_IsPlain(self):
        self.assertEqual(gen.decide("BR", "Barad Nurn", "Barad Nurn", spec())[0], "{.NS}")

    def test_decide_LeadingArticle_IsUndecided(self):
        tag, why = gen.decide("BR", "A Companhia Cinzenta", "The Grey Company", spec())
        self.assertIsNone(tag)
        self.assertIn("article", why)

    def test_decide_ItalianElidedArticle_IsUndecided(self):
        s = spec(plain="{.FN}", articles=["Il", "La", "I", "Gli", "Le", "L'"])
        tag, why = gen.decide("IT", "L'Occhio Cieco", "The Blind Eye", s)
        self.assertIsNone(tag)
        self.assertIn("article", why)

    def test_decide_UntranslatedEnglishWithEnglishHead_IsUndecided(self):
        tag, why = gen.decide("BR", "Kingdom of Cardolan", "Kingdom of Cardolan", spec())
        self.assertIsNone(tag)
        self.assertEqual(why, "untranslated")

    def test_decide_EnglishLookingProperName_IsStillPlain(self):
        self.assertEqual(gen.decide("BR", "Dol Guldur", "Dol Guldur", spec(proper_prefixes=["Dol"]))[0], "{.NS}")

    def test_decide_UnknownFirstWordOfTranslatedMultiWordName_IsUndecided(self):
        tag, why = gen.decide("BR", "Xyzzy Foo", "Xyzzy Bar", spec())
        self.assertIsNone(tag)
        self.assertIn("Xyzzy", why)

    def test_decide_MultiWordNameKeptAsWritten_IsPlain(self):
        self.assertEqual(gen.decide("BR", "Xyzzy Foo", "Xyzzy Foo", spec()), ("{.NS}", "name kept as written"))

    def test_decide_Pin_BeatsEveryRule(self):
        self.assertEqual(gen.decide("BR", "A Mao", "The Hand", spec(), pin="{.FS}"), ("{.FS}", "pinned"))

    def test_decide_AlreadyTaggedText_IsJudgedByItsBody(self):
        self.assertEqual(gen.decide("BR", "{.MS}Erebor", "Erebor", spec())[0], "{.NS}")

    def test_decide_EmptyText_IsUndecided(self):
        self.assertIsNone(gen.decide("BR", "", "", spec())[0])

    def test_decide_PlainNameByCategory_UsesTheCategorysTag(self):
        s = spec(plain="{.FN}", plain_by_category={"kingdom": "{.MN}", "clan": "{.MN}", "minor_faction": "{.MN}"})
        self.assertEqual(gen.decide("IT", "Erebor", "Erebor", s, category="kingdom")[0], "{.MN}")
        self.assertEqual(gen.decide("IT", "Ashmog", "Ashmog", s, category="clan")[0], "{.MN}")
        self.assertEqual(gen.decide("IT", "Ashmog", "Ashmog", s, category="minor_faction")[0], "{.MN}")

    def test_decide_PlainNameOfAnUnlistedCategory_UsesThePlainTag(self):
        s = spec(plain="{.FN}", plain_by_category={"kingdom": "{.MN}"})
        self.assertEqual(gen.decide("IT", "Epicrotea", "Epicrotea", s, category="settlement")[0], "{.FN}")
        self.assertEqual(gen.decide("IT", "Epicrotea", "Epicrotea", s)[0], "{.FN}")

    def test_decide_MultiWordKeptAsWritten_UsesTheCategorysPlainTag(self):
        s = spec(plain="{.FN}", plain_by_category={"clan": "{.MN}"})
        self.assertEqual(gen.decide("IT", "Xyzzy Foo", "Xyzzy Foo", s, category="clan")[0], "{.MN}")

    def test_decide_HeadNounBeatsTheCategorysPlainTag(self):
        s = spec(plain="{.FN}", plain_by_category={"clan": "{.MN}"})
        self.assertEqual(gen.decide("IT", "Casa di Bard", "House of Bard", s, category="clan")[0], "{.FS}")

    def test_decide_ItalianCastleWithoutATitle_TakesTheMasculineNounTag(self):
        s = spec(plain="{.FN}", plain_by_kind={"castle": "{.MN}"})
        self.assertEqual(gen.decide("IT", "Barad Nurn", "Barad Nurn", s, category="settlement", kind="castle")[0], "{.MN}")
        self.assertEqual(gen.decide("IT", "Lithur", "Lithur", s, category="settlement", kind="castle")[0], "{.MN}")

    def test_decide_ItalianTown_StaysFeminineNoun(self):
        s = spec(plain="{.FN}", plain_by_kind={"castle": "{.MN}"})
        self.assertEqual(gen.decide("IT", "Epicrotea", "Epicrotea", s, category="settlement", kind="town")[0], "{.FN}")

    def test_decide_ItalianCastleWithAHeadNoun_KeepsTheNounsTag(self):
        s = spec(plain="{.FN}", plain_by_kind={"castle": "{.MN}"}, head_nouns={"Castello": "{.MS}"})
        self.assertEqual(gen.decide("IT", "Castello di Caleus", "Castle of Caleus", s, category="settlement", kind="castle")[0], "{.MS}")

    def test_decide_CastleVillage_IsNotACastle(self):
        s = spec(plain="{.FN}", plain_by_kind={"castle": "{.MN}"})
        self.assertEqual(gen.decide("IT", "Rhesos", "Rhesos", s, category="settlement", kind="castle_village")[0], "{.FN}")

    def test_decide_NounJoinedByHyphens_GetsTheNounTag(self):
        self.assertEqual(gen.decide("BR", "Prole-das-Rochas", "Crag-spawn", spec()), ("{.FS}", "head noun Prole"))


class BuildTests(unittest.TestCase):
    def test_build_tags_DecidedIdsOnly_AndListsUndecided(self):
        names = [inv.NameKey("a", "kingdom", "name", "Erebor"), inv.NameKey("b", "clan", "name", "The Hand")]
        rows = {"BR": {"a": "Erebor", "b": "A Mao"}}
        table = {"languages": {"BR": spec()}}
        tags, undecided = gen.build(table, names, rows)
        self.assertEqual(tags["BR"], {"a": "{.NS}"})
        self.assertEqual([u.id for u in undecided["BR"]], ["b"])

    def test_build_PinnedId_IsKeptEvenWhenRulesWouldSkipIt(self):
        names = [inv.NameKey("b", "clan", "name", "The Hand")]
        rows = {"BR": {"b": "A Mao"}}
        table = {"languages": {"BR": spec(pins={"b": "{.FS}"})}}
        tags, undecided = gen.build(table, names, rows)
        self.assertEqual(tags["BR"], {"b": "{.FS}"})
        self.assertEqual(undecided["BR"], [])

    def test_build_TagsAreSortedById(self):
        names = [inv.NameKey("z", "kingdom", "name", "Z"), inv.NameKey("a", "kingdom", "name", "A")]
        rows = {"BR": {"z": "Zed", "a": "Aye"}}
        tags, _ = gen.build({"languages": {"BR": spec()}}, names, rows)
        self.assertEqual(list(tags["BR"]), ["a", "z"])

    def test_build_PassesTheCategoryToTheRules(self):
        names = [inv.NameKey("k", "kingdom", "name", "Erebor"), inv.NameKey("s", "settlement", "town", "Roma")]
        rows = {"IT": {"k": "Erebor", "s": "Roma"}}
        s = spec(plain="{.FN}", plain_by_category={"kingdom": "{.MN}"})
        tags, _ = gen.build({"languages": {"IT": s}}, names, rows)
        self.assertEqual(tags["IT"], {"k": "{.MN}", "s": "{.FN}"})

    def test_build_PassesTheSettlementKindToTheRules(self):
        names = [inv.NameKey("c", "settlement", "castle", "Lithur"), inv.NameKey("t", "settlement", "town", "Roma")]
        s = spec(plain="{.FN}", plain_by_kind={"castle": "{.MN}"})
        tags, _ = gen.build({"languages": {"IT": s}}, names, {"IT": {"c": "Lithur", "t": "Roma"}})
        self.assertEqual(tags["IT"], {"c": "{.MN}", "t": "{.FN}"})

    def test_build_ExcludedId_IsUntaggedAndListed(self):
        names = [inv.NameKey("g", "culture", "name", "Gondorian")]
        rows = {"IT": {"g": "Gondoriano"}}
        tags, undecided = gen.build({"languages": {"IT": spec(exclude={"g": "an adjective, not a noun"})}}, names, rows)
        self.assertEqual(tags["IT"], {})
        self.assertEqual([(u.id, u.reason) for u in undecided["IT"]], [("g", "excluded: an adjective, not a noun")])

    def test_build_PinsMustUseAnAllowedTag(self):
        table = {"languages": {"BR": spec(pins={"b": "{.XX}"})}}
        with self.assertRaises(ValueError):
            gen.build(table, [inv.NameKey("b", "clan", "name", "x")], {"BR": {"b": "X"}})


class PruneAndCheckTests(unittest.TestCase):
    P = "Settlements.Settlement.name."

    def test_stale_ids_RetiredIdsAreListed_WithTheInstall(self):
        s = spec(tags={"a": "{.NS}", self.P + "town_X": "{.NS}"}, pins={"gone": "{.FS}"})
        stale = gen.stale_ids(s, computable={"a"}, covers_settlements=True)
        self.assertEqual(stale, [self.P + "town_X", "gone"])

    def test_stale_ids_SettlementIds_AreNotJudgedWithoutTheInstall(self):
        s = spec(tags={"a": "{.NS}", self.P + "town_X": "{.NS}", "taom_old": "{.NS}"})
        self.assertEqual(gen.stale_ids(s, computable={"a"}, covers_settlements=False), ["taom_old"])

    def test_merge_tags_DropsStaleIdsAndKeepsUncoveredSettlements(self):
        s = spec(tags={"a": "{.NS}", "taom_old": "{.NS}", self.P + "town_X": "{.NS}"})
        merged = gen.merge_tags(s, {"a": "{.MS}"}, computable={"a"}, covers_settlements=False)
        self.assertEqual(merged, {"a": "{.MS}", self.P + "town_X": "{.NS}"})

    def test_merge_tags_WithTheInstall_PrunesARetiredSettlement(self):
        s = spec(tags={"a": "{.NS}", self.P + "town_X": "{.NS}"})
        merged = gen.merge_tags(s, {"a": "{.NS}"}, computable={"a"}, covers_settlements=True)
        self.assertEqual(merged, {"a": "{.NS}"})

    def test_merge_tags_ResultIsSortedById(self):
        merged = gen.merge_tags(spec(), {"z": "{.NS}", "a": "{.NS}"}, computable={"a", "z"}, covers_settlements=True)
        self.assertEqual(list(merged), ["a", "z"])

    def test_pinned_article_warnings_NamesAPinOnArticleLedText(self):
        s = spec(pins={"m": "{.MS}", "ok": "{.FS}"})
        rows = {"m": "O Morannon", "ok": "Mao Branca"}
        self.assertEqual(gen.pinned_article_warnings("BR", {"languages": {"BR": s}}, rows), ["m"])


class LiveTextTests(unittest.TestCase):
    """Settlement tags are judged on the live text, because that is what the apply tags."""

    ID = "Settlements.Settlement.name.castle_AN5"

    def _modules(self, td, live_text):
        modules = Path(td) / "Modules"
        write(modules / "TAOM_Map" / "ModuleData" / "settlements.xml",
              f'<S><Settlement id="castle_AN5" name="{{={self.ID}}}Watch" /></S>')
        for lang in ("BR", "IT"):
            write(modules / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml",
                  f'<base><strings><string id="{self.ID}" text="{live_text[lang]}" /></strings></base>')
        return modules

    def test_inventory_rows_SettlementRow_IsTheLiveTextWithoutItsTag(self):
        with tempfile.TemporaryDirectory() as td:
            modules = self._modules(td, {"BR": "{.NS}Vigia do Mitheithel", "IT": "Guardia del Mitheithel"})
            names, rows = gen.inventory_rows(modules)
        self.assertIn(self.ID, [n.id for n in names])
        self.assertEqual(rows["BR"][self.ID], "Vigia do Mitheithel")
        self.assertEqual(rows["IT"][self.ID], "Guardia del Mitheithel")

    def test_build_OnLiveText_GivesTheLiveNamesHeadNounTag(self):
        table = nt.load_table()
        with tempfile.TemporaryDirectory() as td:
            modules = self._modules(td, {"BR": "Vigia do Mitheithel", "IT": "Guardia del Mitheithel"})
            names, rows = gen.inventory_rows(modules)
        tags, _ = gen.build(table, [n for n in names if n.id == self.ID], rows)
        self.assertEqual(tags["BR"][self.ID], "{.FS}")
        self.assertEqual(tags["IT"][self.ID], "{.FS}")

    def test_inventory_rows_SettlementMissingFromTheLiveFile_FallsBackToCacheOrEnglish(self):
        with tempfile.TemporaryDirectory() as td:
            modules = self._modules(td, {"BR": "x", "IT": "x"})
            write(modules / "TAOM_Map" / "ModuleData" / "settlements.xml",
                  f'<S><Settlement name="{{={self.ID}}}Watch" /><Settlement name="{{=Settlements.Settlement.name.town_NEW}}Newtown" /></S>')
            names, rows = gen.inventory_rows(modules)
        self.assertEqual(rows["BR"]["Settlements.Settlement.name.town_NEW"], "Newtown")


class GeneratorMainTests(unittest.TestCase):
    """main() on a temp table and a temp TAOM_Map: the --check stale-id report and the --write prune."""

    P = "Settlements.Settlement.name."

    def _setup(self, td):
        modules = LiveTextTests()._modules(td, {"BR": "Vigia do Mitheithel", "IT": "Guardia del Mitheithel"})
        table = nt.load_table()
        for lang in nt.TAGGED_LANGUAGES:
            spec = table["languages"][lang]
            spec["tags"] = {k: v for k, v in spec["tags"].items() if not k.startswith(self.P)}
            spec["tags"][LiveTextTests.ID] = "{.FS}"
            spec["tags"][self.P + "aaa_gone"] = "{.NS}"
            spec["pins"][self.P + "aaa_gone_pin"] = "{.FS}"
        path = Path(td) / "table.json"
        path.write_text(nt.dump_table(table), encoding="utf-8")
        return modules, path

    def _main(self, modules, path, args):
        import contextlib
        import io
        out, err = io.StringIO(), io.StringIO()
        old = os.environ.get("BANNERLORD_GAME_MODULES")
        os.environ["BANNERLORD_GAME_MODULES"] = str(modules)
        try:
            with mock.patch.object(nt, "TABLE_PATH", path), contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                code = gen.main(args)
        finally:
            if old is None:
                del os.environ["BANNERLORD_GAME_MODULES"]
            else:
                os.environ["BANNERLORD_GAME_MODULES"] = old
        return code, out.getvalue(), err.getvalue()

    def test_main_Check_ReportsAnIdTheInventoryNoLongerLists(self):
        with tempfile.TemporaryDirectory() as td:
            modules, path = self._setup(td)
            code, _, err = self._main(modules, path, ["--check"])
        self.assertEqual(code, 1)
        self.assertIn(self.P + "aaa_gone", err)
        self.assertIn("no longer lists", err)

    def test_main_Write_PrunesRetiredTagsAndPins_ThenCheckPasses(self):
        with tempfile.TemporaryDirectory() as td:
            modules, path = self._setup(td)
            code, _, _ = self._main(modules, path, ["--write"])
            self.assertEqual(code, 0)
            table = nt.load_table(path)
            for lang in nt.TAGGED_LANGUAGES:
                self.assertNotIn(self.P + "aaa_gone", table["languages"][lang]["tags"])
                self.assertNotIn(self.P + "aaa_gone_pin", table["languages"][lang]["pins"])
                self.assertEqual(table["languages"][lang]["tags"][LiveTextTests.ID], "{.FS}")
            code, _, _ = self._main(modules, path, ["--check"])
            self.assertEqual(code, 0)

    def test_main_Write_WithoutAnInstall_KeepsTheSettlementTags(self):
        with tempfile.TemporaryDirectory() as td:
            _, path = self._setup(td)
            code, _, _ = self._main(Path(td) / "no-modules", path, ["--write", "--no-install"])
            self.assertEqual(code, 0)
            table = nt.load_table(path)
            self.assertIn(self.P + "aaa_gone", table["languages"]["BR"]["tags"])


class CheckOutputTests(unittest.TestCase):
    def test_main_CheckWithoutTheInstall_SaysTheSettlementsWereNotChecked(self):
        import contextlib
        import io
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = gen.main(["--check", "--no-install"])
        self.assertEqual(code, 0)
        self.assertIn("Settlements: not checked (no TAOM_Map install)", out.getvalue())


class InventoryTests(unittest.TestCase):
    def test_collect_repo_names_ReadsKingdomClanMinorAndCultureNames(self):
        with tempfile.TemporaryDirectory() as td:
            md = Path(td)
            write(md / "taom_spkingdoms.xml",
                  '<Kingdoms><Kingdom id="k" name="{=taom_k_name}Kingdom of K" short_name="{=taom_k_short}K" '
                  'title="{=taom_k_title}K Realm" ruler_title="{=taom_k_ruler}King" text="{=taom_k_desc}Long." />'
                  '</Kingdoms>')
            write(md / "spkingdoms.xslt", '<x><a name="{{=TAOM_g}}Gondor" short_name="{{=TAOM_g_short}}Gondor"/></x>')
            write(md / "characters" / "clans.xml", '<Factions><Faction id="c" name="{=aom_c_name}House of C"/></Factions>')
            write(md / "spclans.xslt",
                  '<xsl:stylesheet><xsl:template>'
                  '<xsl:attribute name="name">{=TAOM_clan_x_1}Xclan</xsl:attribute>'
                  '<xsl:attribute name="name">{=TAOM_band_name}The Band</xsl:attribute>'
                  '<xsl:attribute name="short_name">{=TAOM_band_short}Band</xsl:attribute>'
                  '<xsl:attribute name="text">{=TAOM_band_text}Story</xsl:attribute>'
                  '</xsl:template></xsl:stylesheet>')
            write(md / "taom_spcultures.xml",
                  '<Cultures><Culture id="dw"\n name="{=aom_dw_name}Dwarves"\n color="1">'
                  '<male_names><name name="{=aom_dw_male_1}Gimli"/></male_names>'
                  '<clan_names><name name="{=aom_dw_clan_1}House of Durin"/></clan_names></Culture></Cultures>')
            write(md / "spcultures.xslt",
                  '<x><xsl:template match="Culture[@id=\'empire\']"><xsl:attribute name="name">'
                  '{=aom_empire_name}Dunlendings</xsl:attribute></xsl:template></x>')
            names = {n.id: n for n in inv.collect_repo_names(md)}
        self.assertEqual(names["taom_k_name"].category, "kingdom")
        self.assertEqual(names["taom_k_short"].attr, "short_name")
        self.assertEqual(names["taom_k_title"].english, "K Realm")
        self.assertNotIn("taom_k_ruler", names)
        self.assertNotIn("taom_k_desc", names)
        self.assertEqual(names["TAOM_g"].category, "kingdom")
        self.assertEqual(names["aom_c_name"].category, "clan")
        self.assertEqual(names["TAOM_clan_x_1"].category, "clan")
        self.assertEqual(names["TAOM_band_name"].category, "minor_faction")
        self.assertEqual(names["TAOM_band_short"].category, "minor_faction")
        self.assertNotIn("TAOM_band_text", names)
        self.assertEqual(names["aom_dw_name"].category, "culture")
        self.assertEqual(names["aom_empire_name"].category, "culture")
        self.assertEqual(names["aom_dw_clan_1"].category, "clan")
        self.assertNotIn("aom_dw_male_1", names)

    def test_collect_repo_names_DuplicateId_IsKeptOnce(self):
        with tempfile.TemporaryDirectory() as td:
            md = Path(td)
            write(md / "taom_spkingdoms.xml", "<Kingdoms/>")
            write(md / "spkingdoms.xslt", "<x/>")
            write(md / "spclans.xslt", "<x/>")
            write(md / "spcultures.xslt", "<x/>")
            write(md / "taom_spcultures.xml", "<Cultures/>")
            write(md / "characters" / "clans.xml",
                  '<F><Faction name="{=a}Alpha"/><Faction name="{=a}Alpha"/></F>')
            names = inv.collect_repo_names(md)
        self.assertEqual([n.id for n in names], ["a"])

    def test_collect_settlement_names_ReadsNameKeysOnly(self):
        with tempfile.TemporaryDirectory() as td:
            f = write(Path(td) / "settlements.xml",
                      '<S><Settlement id="town_A1" name="{=Settlements.Settlement.name.town_A1}Alpha" '
                      'text="{=Settlements.Settlement.text.town_A1}Alpha is a town."/></S>')
            names = inv.collect_settlement_names(f, [])
        self.assertEqual([(n.id, n.category, n.attr, n.english) for n in names],
                         [("Settlements.Settlement.name.town_A1", "settlement", "town", "Alpha")])

    def test_collect_settlement_names_FallsBackToCacheIds(self):
        names = inv.collect_settlement_names(None, ["Settlements.Settlement.name.village_A1_1", "taom_x"])
        self.assertEqual([n.id for n in names], ["Settlements.Settlement.name.village_A1_1"])
        self.assertEqual(names[0].attr, "village")

    def test_collect_lore_names_ReadsTheKeyedTownNamesOfTheLoreList(self):
        with tempfile.TemporaryDirectory() as td:
            f = write(Path(td) / "LoreNames.cs",
                      'new LoreRow("town_ES2", "{=taom_namegiving_lore_minas_ithil}Minas Ithil", FactionSide.Free),\n'
                      'new LoreRow("town_AN2", "{=taom_namegiving_lore_deadmens_dike}Deadmen\'s Dike", FactionSide.Evil),')
            names = inv.collect_lore_names(f)
        self.assertEqual([(n.id, n.category, n.english) for n in names],
                         [("taom_namegiving_lore_minas_ithil", "lore", "Minas Ithil"),
                          ("taom_namegiving_lore_deadmens_dike", "lore", "Deadmen's Dike")])

    def test_equal_to_english_Count_IgnoresTheLeadingTag(self):
        keys = [inv.NameKey("a", "kingdom", "name", "Erebor"), inv.NameKey("b", "kingdom", "name", "Gondor")]
        self.assertEqual(inv.equal_to_english(keys, {"a": "{.NS}Erebor", "b": "Reino de Gondor"}), 1)

    def test_settlement_kind_FromId(self):
        for sid, kind in [("town_EN1", "town"), ("castle_EN3", "castle"), ("castle_village_EN3_1", "castle_village"),
                          ("village_EN1_2", "village"), ("hideout_forest_1", "hideout"), ("zzz", "other")]:
            self.assertEqual(inv.settlement_kind(sid), kind)

    def test_language_rows_MergesFilesAndUnescapes(self):
        with tempfile.TemporaryDirectory() as td:
            ld = Path(td)
            write(ld / "BR" / "std_taom_a_por-BR.xml",
                  '<base><strings><string id="a" text="Cais &amp; Porto"/></strings></base>')
            write(ld / "BR" / "std_taom_b_por-BR.xml",
                  '<base><strings><string id="b" text="{.NS}Beta"/></strings></base>')
            rows = inv.language_rows("BR", ld)
        self.assertEqual(rows, {"a": "Cais & Porto", "b": "{.NS}Beta"})

    def test_resolved_rows_PrefersOverrideThenCacheThenEnglish(self):
        names = [inv.NameKey("o", "settlement", "town", "Eng O"), inv.NameKey("c", "settlement", "town", "Eng C"),
                 inv.NameKey("e", "settlement", "town", "Eng E")]
        rows = inv.resolved_rows(names, overrides={"o": "Over"}, cache={"o": "Cache", "c": "Cached"})
        self.assertEqual(rows, {"o": "Over", "c": "Cached", "e": "Eng E"})

    def test_reconcile_NamesLiveIdsMissingFromCache(self):
        rows = inv.reconcile_live(live_ids=["a", "b", "c"], cache={"a": "x", "c": "y"})
        self.assertEqual(rows, ["b"])


class ShippedTreeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.table = nt.load_table()
        cls.names = inv.collect_repo_names()

    def test_inventory_FindsEveryCategoryThatIsReadFromTheRepo(self):
        cats = {n.category for n in self.names}
        self.assertEqual(cats, {"kingdom", "minor_faction", "clan", "culture"})

    def test_inventory_RowsWithoutAnInstall_KeepTheFourLoreNames(self):
        names, rows = gen.inventory_rows(None)
        lore = [n.id for n in names if n.category == "lore"]
        self.assertEqual(len(lore), 4)
        self.assertTrue(all(i in rows["BR"] and i in rows["IT"] for i in lore))

    def test_table_LoreNamesStillInEnglish_AreExcludedNotTagged(self):
        for lang in nt.TAGGED_LANGUAGES:
            spec = self.table["languages"][lang]
            for key in ("taom_namegiving_lore_deadmens_dike", "taom_namegiving_lore_treegarth"):
                self.assertIn(key, spec["exclude"])
                self.assertNotIn(key, spec["tags"])

    def test_table_LoreNamesThatAreSindarin_AreTaggedAsPlainTownNames(self):
        self.assertEqual(self.table["languages"]["BR"]["tags"]["taom_namegiving_lore_minas_ithil"], "{.NS}")
        self.assertEqual(self.table["languages"]["IT"]["tags"]["taom_namegiving_lore_amon_lanc"], "{.FN}")

    def test_inventory_KingdomCountMatchesTheEnglishSources(self):
        kingdoms = [n for n in self.names if n.category == "kingdom"]
        self.assertEqual(len(kingdoms), 90)

    def test_table_TagsAgreeWithTheGeneratorForRepoNames(self):
        rows = {lang: inv.language_rows(lang) for lang in nt.TAGGED_LANGUAGES}
        tags, _ = gen.build(self.table, self.names, rows)
        for lang in nt.TAGGED_LANGUAGES:
            have = {i: t for i, t in self.table["languages"][lang]["tags"].items() if i in {n.id for n in self.names}}
            self.assertEqual(have, tags[lang], f"{lang}: rerun python tools/generate_name_tags.py --write")

    def test_inventory_rows_WithoutAnInstall_LeaveSettlementsOut(self):
        # Settlement ids and English defaults come from the live TAOM_Map; without it the cache alone cannot say
        # which multi-word names are kept as written, so --check would disagree with the table on a CI machine.
        names, rows = gen.inventory_rows(None)
        self.assertNotIn("settlement", {n.category for n in names})
        self.assertTrue(all(i in rows["BR"] for i in [n.id for n in names]))

    def test_table_ItalianCastles_AreMasculineNounsAndTownsStayFeminine(self):
        prefix = "Settlements.Settlement.name."
        tags = self.table["languages"]["IT"]["tags"]
        castles = {k: v for k, v in tags.items() if k.startswith(prefix + "castle_") and not k.startswith(prefix + "castle_village")}
        self.assertGreater(len(castles), 20)
        self.assertNotIn("{.FN}", castles.values())
        self.assertIn("{.MN}", castles.values())
        towns = {k: v for k, v in tags.items() if k.startswith(prefix + "town_")}
        self.assertIn("{.FN}", towns.values())
        self.assertNotIn("{.MN}", towns.values())

    def test_table_BrazilianHyphenatedNounClan_NeedsNoPin(self):
        spec = self.table["languages"]["BR"]
        self.assertNotIn("aom_clan_bluecraig_2_name", spec["pins"])
        self.assertEqual(spec["tags"]["aom_clan_bluecraig_2_name"], "{.FS}")

    def test_table_EveryTagIsAGrammarTagToken(self):
        for lang in nt.TAGGED_LANGUAGES:
            for sid, tag in self.table["languages"][lang]["tags"].items():
                self.assertRegex(tag, r"^\{\.[A-Z]{2}\}$")


if __name__ == "__main__":
    unittest.main()
