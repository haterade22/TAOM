using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Pins agreement between the two halves of a named lord: the data that CREATES him
/// (characters/lords.xml + lords.xslt) and the data that DESCRIBES him to the player
/// (characters/heroes.xml + heroes.xslt).
///
/// Why this exists: the two halves were authored from different rosters and never reconciled.
/// A scripted sweep on 2026-08-29 found 77 lords whose biography named somebody else, 12 whose
/// biography contradicted is_female, two parent links pointing at a parent of the wrong sex, and
/// one lord shipping as "RandomDude" in twelve languages. Grima Wormtongue was married to Eowyn
/// because heroes.xslt pasted her biography onto his wife's id and the template inherited vanilla's
/// marriage; Erkenbrand was married to his own bearded son.
///
/// LordNameAndSexConsistencyTests, the only prior coverage here, cannot see any of it: it never
/// opens either heroes file, and its NPCCharacter regex requires id first and name second, which
/// matches barely half of characters/lords.xml.
///
/// Data-only, needs no game and no Bannerlord install, runs in milliseconds.
/// </summary>
[TestClass]
public class LordIdentityConsistencyTests
{
    private sealed class Lord
    {
        public string Id = "";
        public string Key = "";
        public string Name = "";
        public bool IsFemale;
        public int? Age;
        public string Culture = "";
        public string Source = "";
    }

    private sealed class HeroRow
    {
        public string Id = "";
        public string Bio = "";
        public string? Father;
        public string? Mother;
        public string? Spouse;
        public string Source = "";
    }

    /// <summary>
    /// Ids whose biography deliberately never repeats the lord's own name.
    /// Keep this list tiny and justify every entry: the default is that a bio names its subject.
    /// </summary>
    private static readonly HashSet<string> BiographyNeedNotNameTheLord = new(StringComparer.Ordinal)
    {
    };

    // ---------------------------------------------------------------- helpers

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ReadModuleData(string root, params string[] parts)
    {
        var path = Path.Combine(new[] { root, "Main", "_Module", "ModuleData" }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"not found at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Folds the diacritics and digraphs TAOM actually uses so "Theoden" compares equal to
    /// "Theoden" with its accent, and "Aelle Aethellafing" to the AE-ligature spelling.
    /// </summary>
    private static string Fold(string s)
    {
        var expanded = s
            .Replace("Æ", "Ae").Replace("æ", "ae")
            .Replace("Ð", "D").Replace("ð", "d")
            .Replace("Þ", "Th").Replace("þ", "th")
            .Replace("Ø", "O").Replace("ø", "o");

        var decomposed = expanded.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string StripKey(string value) => Regex.Replace(value, @"^\{=[^}]*\}", "").Trim();

    private static string? KeyOf(string value)
    {
        var m = Regex.Match(value, @"^\{=([^}]*)\}");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Reads an attribute out of an element's attribute soup, order independent.</summary>
    private static string? Attr(string tag, string name)
    {
        var m = Regex.Match(tag, $@"\b{Regex.Escape(name)}=""(?<v>[^""]*)""");
        return m.Success ? m.Groups["v"].Value : null;
    }

    /// <summary>Reads an xsl:attribute out of a template body.</summary>
    private static string? XslAttr(string body, string name)
    {
        var m = Regex.Match(
            body,
            $@"<xsl:attribute name=""{Regex.Escape(name)}"">(?<v>.*?)</xsl:attribute>",
            RegexOptions.Singleline);
        return m.Success ? m.Groups["v"].Value.Trim() : null;
    }

    /// <summary>
    /// Every lord the engine ends up with, keyed by id. lords.xslt is loaded first
    /// (SubModule.xml:96), characters/lords.xml last (:157), so the plain XML wins on a
    /// duplicate id. Attribute-order independent, unlike the older test's regex.
    /// </summary>
    private static Dictionary<string, Lord> LoadLords(string root)
    {
        var lords = new Dictionary<string, Lord>(StringComparer.Ordinal);

        var fromXslt = 0;
        var fromXml = 0;
        var seenInXml = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        var xslt = ReadModuleData(root, "lords.xslt");
        foreach (Match m in Regex.Matches(
            xslt,
            @"<xsl:template match=""NPCCharacter\[@id='(?<id>[^']+)'\]"">(?<body>.*?)</xsl:template>",
            RegexOptions.Singleline))
        {
            var body = m.Groups["body"].Value;
            var name = XslAttr(body, "name") ?? "";
            var female = XslAttr(body, "is_female");
            lords[m.Groups["id"].Value] = new Lord
            {
                Id = m.Groups["id"].Value,
                Key = KeyOf(name) ?? "",
                Name = StripKey(name),
                IsFemale = string.Equals(female, "true", StringComparison.OrdinalIgnoreCase),
                Age = int.TryParse(XslAttr(body, "age"), out var xsltAge) ? xsltAge : (int?)null,
                Culture = (XslAttr(body, "culture") ?? "").Replace("Culture.", ""),
                Source = "lords.xslt",
            };
            fromXslt++;
        }

        // MBObjectManager.MergeElementAttributes merges PER ATTRIBUTE, not per node: only the
        // attributes the later file actually declares overwrite the accumulated element, and
        // anything it omits survives from the stylesheet's output. So a lord defined in both files
        // keeps lords.xslt's is_female whenever characters/lords.xml does not state one. Seventeen
        // ids are in exactly that position today; all seventeen happen to say "false" on both
        // sides, but modelling this as a whole-node replace would read the next one wrong.
        var xml = ReadModuleData(root, "characters", "lords.xml");
        foreach (Match m in Regex.Matches(xml, @"<NPCCharacter\b(?<attrs>[^>]*)>"))
        {
            var attrs = m.Groups["attrs"].Value;
            var id = Attr(attrs, "id");
            if (id == null) continue;

            var merged = lords.TryGetValue(id, out var prior)
                ? new Lord { Id = id, Key = prior.Key, Name = prior.Name, IsFemale = prior.IsFemale, Age = prior.Age, Culture = prior.Culture }
                : new Lord { Id = id };
            merged.Source = prior == null ? "characters/lords.xml" : "characters/lords.xml over lords.xslt";

            var name = Attr(attrs, "name");
            if (name != null)
            {
                merged.Key = KeyOf(name) ?? "";
                merged.Name = StripKey(name);
            }

            var ageAttr = Attr(attrs, "age");
            if (ageAttr != null && double.TryParse(ageAttr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsedAge))
                merged.Age = (int)parsedAge;

            var culture = Attr(attrs, "culture");
            if (culture != null) merged.Culture = culture.Replace("Culture.", "");

            var female = Attr(attrs, "is_female");
            if (female != null)
                merged.IsFemale = string.Equals(female, "true", StringComparison.OrdinalIgnoreCase);

            lords[id] = merged;
            if (!seenInXml.Add(id))
                duplicates.Add("  characters/lords.xml declares " + id + " more than once");
            fromXml++;
        }

        Assert.AreEqual(0, duplicates.Count,
            "A duplicate id inside one file is swallowed by the last write, so one of the two\n" +
            "definitions never reaches any gate.\n" + string.Join("\n", duplicates));
        Assert.IsTrue(fromXslt >= 380,
            $"matched only {fromXslt} NPCCharacter templates in lords.xslt; the template shape changed and this gate is reading almost nothing");
        Assert.IsTrue(fromXml >= 1150,
            $"matched only {fromXml} NPCCharacter elements in characters/lords.xml; the element shape changed");
        return lords;
    }

    /// <summary>
    /// Every hero row TAOM declares. Only attributes TAOM writes itself are visible here; a
    /// heroes.xslt template that does not strip an attribute inherits vanilla's value, which the
    /// repo cannot see. That inheritance is exactly why the biography checks below matter: once a
    /// bio names the lord it is actually attached to, the inherited vanilla family is correct too.
    /// </summary>
    private static Dictionary<string, HeroRow> LoadHeroes(string root)
    {
        var heroes = new Dictionary<string, HeroRow>(StringComparer.Ordinal);

        var heroesFromXslt = 0;
        var xslt = ReadModuleData(root, "heroes.xslt");
        foreach (Match m in Regex.Matches(
            xslt,
            @"<xsl:template match=""Hero\[@id='(?<id>[^']+)'\]"">(?<body>.*?)</xsl:template>",
            RegexOptions.Singleline))
        {
            var body = m.Groups["body"].Value;
            heroes[m.Groups["id"].Value] = new HeroRow
            {
                Id = m.Groups["id"].Value,
                Bio = StripKey(XslAttr(body, "text") ?? ""),
                Father = StripHeroPrefix(XslAttr(body, "father")),
                Mother = StripHeroPrefix(XslAttr(body, "mother")),
                Spouse = StripHeroPrefix(XslAttr(body, "spouse")),
                Source = "heroes.xslt",
            };
            heroesFromXslt++;
        }

        var heroesFromXml = 0;
        var xml = ReadModuleData(root, "characters", "heroes.xml");
        foreach (Match m in Regex.Matches(xml, @"<Hero\b(?<attrs>[^>]*?)/>", RegexOptions.Singleline))
        {
            var attrs = m.Groups["attrs"].Value;
            var id = Attr(attrs, "id");
            if (id == null) continue;
            // Per attribute, for the same reason LoadLords is: five ids are declared in both files,
            // and a wholesale replace drops a spouse that only heroes.xslt states.
            heroes.TryGetValue(id, out var prior);
            heroes[id] = new HeroRow
            {
                Id = id,
                Bio = Attr(attrs, "text") is { } t ? StripKey(t) : (prior?.Bio ?? ""),
                Father = StripHeroPrefix(Attr(attrs, "father")) ?? prior?.Father,
                Mother = StripHeroPrefix(Attr(attrs, "mother")) ?? prior?.Mother,
                Spouse = StripHeroPrefix(Attr(attrs, "spouse")) ?? prior?.Spouse,
                Source = prior == null ? "characters/heroes.xml" : "characters/heroes.xml over heroes.xslt",
            };
            heroesFromXml++;
        }

        // A floor of 1000 against 1400 rows left the whole stylesheet half able to stop matching
        // while the gate still reported green on the plain XML alone. Count each source.
        Assert.IsTrue(heroesFromXslt >= 380,
            $"matched only {heroesFromXslt} Hero templates in heroes.xslt; the template shape changed and this gate is reading almost nothing");
        Assert.IsTrue(heroesFromXml >= 980,
            $"matched only {heroesFromXml} Hero elements in characters/heroes.xml; the element shape changed");
        return heroes;
    }

    private static string? StripHeroPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value!.Trim();
        return v.StartsWith("Hero.", StringComparison.Ordinal) ? v.Substring(5) : v;
    }

    /// <summary>The given name: the first word of at least three letters.</summary>
    private static string GivenName(string fullName)
    {
        foreach (var token in Regex.Split(Fold(fullName), @"[^A-Za-z]+"))
            if (token.Length >= 3) return token;
        return "";
    }

    /// <summary>
    /// The spellings a biography may use to identify this lord: the given name, plus any
    /// comma-separated epithet. The Nazgul are named "Nazgul, The Knight of Umbar" and their bios
    /// open with the epithet alone, which identifies them just as well.
    /// </summary>
    private static IEnumerable<string> NameForms(string fullName)
    {
        var given = GivenName(fullName);
        if (given.Length >= 3) yield return given;

        foreach (var segment in fullName.Split(','))
        {
            var epithet = Regex.Replace(segment.Trim(), @"^(?:The|A|An)\s+", "", RegexOptions.IgnoreCase);
            if (epithet.Length >= 5) yield return Fold(epithet);
        }
    }

    // ---------------------------------------------------------------- tests

    [TestMethod]
    public void EveryBiographyNamesTheLordItIsAttachedTo()
    {
        var root = FindRepoRoot();
        var lords = LoadLords(root);
        var heroes = LoadHeroes(root);

        var offenders = new List<string>();
        foreach (var hero in heroes.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (hero.Bio.Length == 0) continue;
            if (BiographyNeedNotNameTheLord.Contains(hero.Id)) continue;
            if (!lords.TryGetValue(hero.Id, out var lord) || lord.Name.Length == 0) continue;

            var forms = NameForms(lord.Name).ToList();
            if (forms.Count == 0) continue;

            var foldedBio = Fold(hero.Bio);
            if (!forms.Any(f => foldedBio.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var excerpt = hero.Bio.Length > 70 ? hero.Bio.Substring(0, 70) + "..." : hero.Bio;
                offenders.Add($"  {hero.Id}: data creates \"{lord.Name}\" ({lord.Source}) " +
                              $"but the bio ({hero.Source}) reads \"{excerpt}\"");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "These lords are described as somebody else. The encyclopedia shows the biography next\n" +
            "to the name, so the player reads both at once. Either the biography belongs on a\n" +
            "different id (reattach it) or the lord was never renamed (rename him). Do not silence\n" +
            "this by adding to BiographyNeedNotNameTheLord unless the bio is genuinely descriptive.\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void EveryBiographySpellsTheLordsNameWithItsDiacritics()
    {
        var root = FindRepoRoot();
        var lords = LoadLords(root);
        var heroes = LoadHeroes(root);

        var offenders = new List<string>();
        foreach (var hero in heroes.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (hero.Bio.Length == 0) continue;
            if (!lords.TryGetValue(hero.Id, out var lord) || lord.Name.Length == 0) continue;

            // The accented given name exactly as the lord data spells it.
            var accented = Regex.Split(lord.Name, @"[\s,]+").FirstOrDefault(t => GivenName(t).Length >= 3);
            if (accented == null) continue;
            if (string.Equals(accented, Fold(accented), StringComparison.Ordinal)) continue; // nothing to lose

            if (hero.Bio.IndexOf(accented, StringComparison.Ordinal) < 0 &&
                Fold(hero.Bio).IndexOf(Fold(accented), StringComparison.OrdinalIgnoreCase) >= 0)
            {
                offenders.Add($"  {hero.Id}: lord data spells it \"{accented}\", the bio ({hero.Source}) strips the accents");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "These biographies spell the lord's name with its diacritics stripped. This is an\n" +
            "English-only defect: the twelve translated bios already carry the accents, so only the\n" +
            "inline English fallback is wrong. Restore the accented spelling.\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void EveryDeclaredParentIsTheRightSex()
    {
        var root = FindRepoRoot();
        var lords = LoadLords(root);
        var heroes = LoadHeroes(root);

        var offenders = new List<string>();
        foreach (var hero in heroes.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            Check(hero.Father, expectFemale: false, role: "father");
            Check(hero.Mother, expectFemale: true, role: "mother");

            void Check(string? parentId, bool expectFemale, string role)
            {
                if (parentId == null || !lords.TryGetValue(parentId, out var parent)) return;
                if (parent.IsFemale == expectFemale) return;
                offenders.Add($"  {hero.Id} ({hero.Source}) has {role}={parentId}, " +
                              $"but \"{parent.Name}\" is is_female={parent.IsFemale.ToString().ToLowerInvariant()}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "A father must be male and a mother female. Fix whichever half is wrong: the link in\n" +
            "the heroes file, or is_female in the lords file (remembering that is_female, the\n" +
            "beard_tags block and the BodyProperties key move as a unit).\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void EveryDeclaredMarriageIsBetweenOppositeSexes()
    {
        var root = FindRepoRoot();
        var lords = LoadLords(root);
        var heroes = LoadHeroes(root);

        var offenders = new List<string>();
        foreach (var hero in heroes.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (hero.Spouse == null) continue;
            if (!lords.TryGetValue(hero.Id, out var self)) continue;
            if (!lords.TryGetValue(hero.Spouse, out var spouse)) continue;
            if (self.IsFemale != spouse.IsFemale) continue;

            var sex = self.IsFemale ? "female" : "male";
            offenders.Add($"  {hero.Id} \"{self.Name}\" and {hero.Spouse} \"{spouse.Name}\" " +
                          $"are married ({hero.Source}) and both {sex}");
        }

        Assert.AreEqual(0, offenders.Count,
            "Bannerlord marriages are opposite-sex. Where both partners read male, the wife's\n" +
            "entry is usually the vanilla male id she was reused from, still carrying is_female=\n" +
            "\"false\" and a beard_tags block while her biography says \"she\" throughout.\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void EveryMarriageIsReciprocal()
    {
        var root = FindRepoRoot();
        var heroes = LoadHeroes(root);

        var offenders = new List<string>();
        foreach (var hero in heroes.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (hero.Spouse == null) continue;
            if (!heroes.TryGetValue(hero.Spouse, out var partner)) continue;
            // Only judge a partner TAOM also writes a spouse for; vanilla inheritance is invisible here.
            if (partner.Spouse == null) continue;
            if (string.Equals(partner.Spouse, hero.Id, StringComparison.Ordinal)) continue;

            offenders.Add($"  {hero.Id} is married to {hero.Spouse}, but {hero.Spouse} is married to {partner.Spouse}");
        }

        Assert.AreEqual(0, offenders.Count,
            "A marriage TAOM declares on both partners must point back. A one-way link left over\n" +
            "from a reattached biography is how Erkenbrand ended up married to his own son.\n" +
            string.Join("\n", offenders));
    }

    [TestMethod]
    public void NoHeroTreatsTheSameLordAsBothFatherAndHusband()
    {
        var root = FindRepoRoot();
        var heroes = LoadHeroes(root);

        var offenders = heroes.Values
            .Where(h => h.Spouse != null && string.Equals(h.Spouse, h.Father, StringComparison.Ordinal))
            .OrderBy(h => h.Id, StringComparer.Ordinal)
            .Select(h => $"  {h.Id} ({h.Source}) has father and spouse both = {h.Spouse}")
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "This is the Malrior defect a00086da fixed: a heroes.xslt template added a spouse but\n" +
            "let vanilla's father attribute survive the copy, so one lord was both. A template that\n" +
            "assigns a spouse to a reused vanilla id must strip father as well.\n" +
            string.Join("\n", offenders));
    }

    /// <summary>
    /// Lords with no &lt;Hero&gt; entry are authored and then never instantiated, so they never enter a
    /// campaign at all. Eowyn Eoforing sat in that state for as long as the file has existed.
    /// The exclusions below are lords whose clan cannot be derived from the data; assigning one
    /// would be authoring new content rather than reconciling what is already there.
    /// </summary>
    private static readonly HashSet<string> LordsDeliberatelyWithoutAHero = new(StringComparer.Ordinal)
    {
        // An undifferentiated pool of Gondor-west lords, every one authored under a "Placeholder
        // face" comment and never assigned to a house. Six of their neighbours (EW_1, 6, 9, 14,
        // 20, 23) did get clans; nothing in the repo says which house these twenty-two belong to.
        "lord_EW_2", "lord_EW_3", "lord_EW_4", "lord_EW_5", "lord_EW_7", "lord_EW_8",
        "lord_EW_10", "lord_EW_11", "lord_EW_12", "lord_EW_13", "lord_EW_15", "lord_EW_16",
        "lord_EW_17", "lord_EW_18", "lord_EW_19", "lord_EW_21", "lord_EW_22", "lord_EW_24",
        "lord_EW_25", "lord_EW_26", "lord_EW_27", "lord_EW_28",

        // A second lord named Duilin. lord_WE9_u is the one the registry and all twelve language
        // files call Duilin, and the one whose biography names him elder son of Duinhir, so giving
        // this one a Hero entry as well would put two Duilins in Morthond.
        "lord_WE9_l_1",
    };

    [TestMethod]
    public void EveryLordEitherSpawnsOrIsAKnownExclusion()
    {
        var root = FindRepoRoot();
        var lords = LoadLords(root);
        var heroes = LoadHeroes(root);

        var orphans = lords.Keys
            .Where(id => !heroes.ContainsKey(id))
            .Where(id => !LordsDeliberatelyWithoutAHero.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => $"  {id} \"{lords[id].Name}\" ({lords[id].Source})")
            .ToList();

        Assert.AreEqual(0, orphans.Count,
            "These lords are defined but have no <Hero> entry, so the campaign never creates them.\n" +
            "Give each one a faction and its family wiring in characters/heroes.xml, or add it to\n" +
            "LordsDeliberatelyWithoutAHero with the reason.\n" +
            string.Join("\n", orphans));

        var stale = LordsDeliberatelyWithoutAHero
            .Where(id => heroes.ContainsKey(id) || !lords.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        Assert.AreEqual(0, stale.Count,
            "These ids are excused from needing a Hero entry but no longer need excusing:\n  " +
            string.Join("\n  ", stale));
    }

    /// <summary>
    /// Names held by two spawning lords of the SAME culture. Reuse across cultures is deliberate
    /// (the goblin, Misty Mountain and Blue Craig rosters share an orc name pool on purpose), so
    /// this is scoped to one culture, where two lords a player can meet in the same war really do
    /// answer to one name. It also defeats every biography check in this file, since a bio naming
    /// one of them satisfies the other.
    ///
    /// Shrink-only. Nineteen of these predate 2026-08-29; the five Gondor entries appeared when
    /// that pass gave Imrahil's children Hero entries and they began to spawn alongside unrelated
    /// lords who already held their names. Fixing those five means renaming a lord, which is a
    /// content decision nobody has taken.
    /// </summary>
    private static readonly HashSet<string> AcceptedDuplicateNames = new(StringComparer.Ordinal)
    {
        // Surfaced 2026-08-29 by the new Hero entries. Canonical children of Imrahil against
        // unrelated Gondor lords: lord_1_9_1/2/3 vs lord_1_25/1_35/1_24, lord_1_11_4 and
        // lord_1_52_3 vs lord_1_36, lord_1_45_5 vs lord_1_73_1.
        "Elphir", "Erchirion", "Amrothos", "Ivriniel", "Belwen",

        // Pre-existing. Father and son both Wulf Celmunding, which is what makes lord_4_22_1's
        // biography ("Sunnifa is wife to Wulf") ambiguous. Dorwen is lord_EW_1_3 against
        // lord_WE8_1, both Gondor.
        "Wulf Celmunding",
        "Dorwen",

        // Pre-existing orc and goblin rosters reusing a name inside one culture.
        "Borzak", "Dushgar", "Gashnar", "Gorbag", "Grizznak", "Grukhash", "Hrakdush", "Lagduf",
        "Lugdush", "Mauhur", "Mughash", "Skarsnik", "Thalka", "Ufthak", "Ulgrim", "Urzok", "Vorzul",
    };

    [TestMethod]
    public void NoTwoSpawningLordsOfOneCultureShareADisplayName()
    {
        var root = FindRepoRoot();
        var lords = LoadLords(root);
        var heroes = LoadHeroes(root);

        var groups = lords.Values
            .Where(l => l.Name.Length > 0 && l.Culture.Length > 0 && heroes.ContainsKey(l.Id))
            .GroupBy(l => (l.Name, l.Culture))
            .Where(g => g.Count() > 1 && !AcceptedDuplicateNames.Contains(g.Key.Name))
            .OrderBy(g => g.Key.Name, StringComparer.Ordinal)
            .Select(g => $"  \"{g.Key.Name}\" ({g.Key.Culture}): " +
                         string.Join(", ", g.Select(l => l.Id).OrderBy(i => i, StringComparer.Ordinal)))
            .ToList();

        Assert.AreEqual(0, groups.Count,
            "Two lords of one culture that both spawn share a display name. Every biography check\n" +
            "in this file matches on the name, so a biography about one silently satisfies the\n" +
            "other. Rename one, or add the name to AcceptedDuplicateNames with the reason.\n" +
            string.Join("\n", groups));

        // Shrink-only, like the other two exception lists in this suite. Without it a name stays
        // excused long after the collision has gone, and renaming the Haradrim Duilin, Haldir,
        // Rumil, Orophin and Calemir on 2026-08-29 is exactly the repair that leaves one behind.
        var live = new HashSet<string>(
            lords.Values
                .Where(l => l.Name.Length > 0 && l.Culture.Length > 0 && heroes.ContainsKey(l.Id))
                .GroupBy(l => (l.Name, l.Culture))
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.Name),
            StringComparer.Ordinal);

        var stale = AcceptedDuplicateNames.Except(live).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, stale.Count,
            "These names are excused in AcceptedDuplicateNames but no longer collide. Remove them;\n" +
            "the list may only ever shrink.\n  " + string.Join("\n  ", stale));
    }

    /// <summary>
    /// A parent too young to be one. Parentage inferred from an id pattern rather than from the
    /// ages is the likeliest place for a wrong guess to land, and nothing else checks it: two of
    /// the links added on 2026-08-29 had a gap of zero and one year. Fourteen years is deliberately
    /// permissive, so this only catches links that are impossible, not ones that are merely young.
    /// </summary>
    [TestMethod]
    public void NoParentIsTooYoungToBeOne()
    {
        var root = FindRepoRoot();
        var lords = LoadLords(root);
        var heroes = LoadHeroes(root);

        var baselinePath = Path.Combine(root, "TAOM.Tests", "Core", "impossible-age-links-baseline.txt");
        Assert.IsTrue(File.Exists(baselinePath), $"baseline not found at {baselinePath}");
        var baseline = new HashSet<string>(
            File.ReadAllLines(baselinePath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)),
            StringComparer.Ordinal);

        var faults = new List<string>();
        var stillBaselined = new HashSet<string>(StringComparer.Ordinal);

        foreach (var hero in heroes.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (!lords.TryGetValue(hero.Id, out var child) || child.Age == null) continue;
            foreach (var link in new[] { ("father", hero.Father), ("mother", hero.Mother) })
            {
                if (link.Item2 == null || !lords.TryGetValue(link.Item2, out var parent)) continue;
                if (parent.Age == null || parent.Age - child.Age >= 14) continue;

                var key = $"{hero.Id}|{link.Item1}|{link.Item2}";
                if (baseline.Contains(key)) { stillBaselined.Add(key); continue; }
                faults.Add($"  {hero.Id} \"{child.Name}\" is {child.Age}, {link.Item1} {link.Item2} " +
                           $"\"{parent.Name}\" is {parent.Age}");
            }
        }

        Assert.AreEqual(0, faults.Count,
            "These parent links are impossible on age. Either the ages are wrong or the parentage\n" +
            "is; an id pattern is not evidence of descent.\n" + string.Join("\n", faults));

        var repaired = baseline.Except(stillBaselined).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, repaired.Count,
            "These links are baselined but are no longer violations. The list is shrink-only.\n" +
            "Delete these lines from impossible-age-links-baseline.txt:\n  " +
            string.Join("\n  ", repaired));
    }

    [TestMethod]
    public void EveryLordNameInLordsXsltMatchesTheRegistry()
    {
        var root = FindRepoRoot();
        var registry = ReadModuleData(root, "taom_xslt_strings.xml");
        var xslt = ReadModuleData(root, "lords.xslt");

        var registered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(
            registry, @"<string id=""(?<id>[^""]+)"" text=""(?:\{=\k<id>\})?(?<text>[^""]*)"" />"))
        {
            registered[m.Groups["id"].Value] = m.Groups["text"].Value;
        }
        Assert.IsTrue(registered.Count > 1000, $"parsed only {registered.Count} registry rows; the file shape changed");

        var missing = new List<string>();
        var drift = new List<string>();
        var untrimmed = new List<string>();

        foreach (Match m in Regex.Matches(
            xslt, @"<xsl:attribute name=""name"">\{=(?<key>[^}]+)\}(?<literal>[^<]*)</xsl:attribute>"))
        {
            var key = m.Groups["key"].Value;
            var literal = m.Groups["literal"].Value;

            if (literal != literal.Trim()) untrimmed.Add($"  {key}: \"{literal}\"");

            if (!registered.TryGetValue(key, out var text)) { missing.Add("  " + key); continue; }
            if (!string.Equals(literal.Trim(), text, StringComparison.Ordinal))
                drift.Add($"  {key}: lords.xslt says \"{literal.Trim()}\", registry says \"{text}\"");
        }

        Assert.AreEqual(0, missing.Count,
            "lords.xslt names a key that taom_xslt_strings.xml does not register, so the twelve\n" +
            "language files will never carry it and only English players see the name.\n" +
            string.Join("\n", missing.Distinct()));

        // Stray whitespace here is not cosmetic. taom_xslt_strings.xml is generated from this file,
        // so any tool that trims one side of a comparison and not the other reads the key as a
        // changed string. On 2026-08-29 that misread reset six names to English in all twelve
        // languages and destroyed real translations: German "Nazgul, der Dunkle Marschall" became
        // "Nazgul, The Dark Marshall", and "Gorwulf, der Eber" became "Gorwulf, The Boar".
        Assert.AreEqual(0, untrimmed.Count,
            "These lords.xslt name values carry leading or trailing whitespace:\n" +
            string.Join("\n", untrimmed));

        Assert.AreEqual(0, drift.Count,
            "lords.xslt and taom_xslt_strings.xml disagree on a lord's name. The registry is\n" +
            "generated from the stylesheet, so they must agree; the twelve language files are keyed\n" +
            "off the registry, and English players read the stylesheet's own literal.\n" +
            string.Join("\n", drift));
    }
}
