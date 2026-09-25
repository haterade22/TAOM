using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// Two rules for shipped translations: no character that is itself a decoding failure, and no
/// word from another language's writing system.
///
/// <para><b>Decoding failures.</b></para>
/// U+FFFD REPLACEMENT CHARACTER is what a decoder emits when it is handed bytes it cannot
/// interpret. Its presence in a string means the text was damaged at some earlier point and
/// the damage was then written out as if it were content — the player sees a black diamond
/// or an empty box in the middle of a sentence.
///
/// Four rows reached <c>HEAD</c> this way and shipped: one Japanese culture description, one
/// Korean character-creation option, and the Korean forest-people entry in two files. The
/// poisoned text was in <c>tools/translation_cache/</c> too, so every re-run served it straight
/// back — a re-translation could not clear it without the cache entry being purged first.
///
/// This does not assert where the damage came from. The cache reads and writes clean UTF-8 and
/// the only <c>errors="replace"</c> in the translator is on the stdout wrapper, which never
/// touches data, so the origin is unproven — most likely a malformed token from the model or a
/// much older revision of the pipeline. The gate is worth having either way: whatever produces
/// a U+FFFD, it must not reach a player, and detection does not require knowing the source.
///
/// This rule is scoped to the replacement character and C0 controls. Every other non-ASCII
/// codepoint in these files is legitimate — the twelve languages include four non-Latin
/// scripts, and a broad "suspicious character" rule would report thousands of correct rows.
///
/// <para><b>Wrong writing system</b> (2026-09-25).</para> The translator model sometimes emits a word
/// in another script: Korean inside Turkish, Latin letters glued into Cyrillic or Chinese words. That
/// rule stays quiet on correct rows by naming, per language, only the scripts that cannot belong
/// there (<see cref="ForeignScripts"/>), letting product and key names stay Latin
/// (<see cref="LatinAllowed"/>), and stripping placeholders, markup and a leading [culture] tag
/// first. It runs over the repo's language files and over <c>tools/translation_cache/</c>, which
/// also holds the two unversioned modules' translations and serves any damage back on the next run.
/// </summary>
[TestClass]
public class LanguageTextIntegrityTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static string LanguagesPath =>
        Path.Combine(RepoRoot, "Main", "_Module", "ModuleData", "Languages");

    private const char Replacement = '�';

    /// <summary>
    /// Tab, newline and carriage return are legitimate in text; the rest of C0 is not, and a
    /// stray one is the same class of damage as U+FFFD — a byte that survived into content.
    /// </summary>
    private static bool IsIllegalControl(char c) =>
        c < 0x20 && c != '\t' && c != '\n' && c != '\r';

    [TestMethod]
    public void NoTranslatedString_ContainsAReplacementCharacterOrControlCode()
    {
        Assert.IsTrue(Directory.Exists(LanguagesPath), $"Languages not found at {LanguagesPath}");

        var files = Directory.GetFiles(LanguagesPath, "std_taom_*.xml", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 100,
            $"Only {files.Length} language files found — the scan is broken, and a test that " +
            "inspects nothing would pass for the wrong reason.");

        var offenders = new List<string>();
        foreach (var file in files)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch (System.Xml.XmlException)
            {
                continue;   // well-formedness is LanguageDataXmlTests' job, not this one
            }

            foreach (var row in doc.Root.DescendantsAndSelf()
                         .Where(e => e.Name.LocalName == "string"))
            {
                var text = (string)row.Attribute("text") ?? string.Empty;
                var bad = text.Contains(Replacement) ? "U+FFFD"
                    : text.Any(IsIllegalControl) ? "control code"
                    : null;
                if (bad == null)
                {
                    continue;
                }

                var index = text.IndexOf(Replacement);
                if (index < 0)
                {
                    index = text.ToList().FindIndex(IsIllegalControl);
                }
                var start = Math.Max(0, index - 15);
                var excerpt = text.Substring(start, Math.Min(30, text.Length - start));
                offenders.Add(
                    $"  {Path.GetFileName(Path.GetDirectoryName(file))}/{Path.GetFileName(file)} " +
                    $"[{(string)row.Attribute("id")}] {bad} near: …{excerpt}…");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Translations contain characters that are themselves decoding failures. The player " +
            "sees a box or diamond mid-sentence.\n" + string.Join("\n", offenders) +
            "\n\nThe matching tools/translation_cache/<lang>.json entry is almost certainly " +
            "poisoned too — purge it, or the next run serves the same damage straight back.");
    }

    // ── Writing systems ───────────────────────────────────────────────────────────────────────
    //
    // The translator model occasionally emits a word from the wrong writing system: a Korean
    // 돌진 inside Turkish sentences, Chinese 氏族 for "clan" in Turkish, "[Ривенделл]新obranец" in
    // Russian, "黑numenor" in Chinese, Latin look-alike letters inside Cyrillic words ("Брандa",
    // "Бûрзгâш"). The placeholder check cannot see any of it, and the cache serves the damage back
    // on every re-run. 2026-09-25: about 120 such rows across nine languages, all repaired.

    private static readonly HashSet<string> LatinScriptLanguages =
        new HashSet<string> { "BR", "DE", "FR", "IT", "PL", "SP", "TR" };

    private static readonly Dictionary<string, string[]> ForeignScripts = new Dictionary<string, string[]>
    {
        ["RU"] = new[] { "Han", "Kana", "Hangul" },
        ["KO"] = new[] { "Kana", "Cyrillic" },
        ["JP"] = new[] { "Hangul", "Cyrillic" },
        ["CNs"] = new[] { "Hangul", "Kana", "Cyrillic" },
        ["CNt"] = new[] { "Hangul", "Kana", "Cyrillic" },
    };

    /// <summary>Product and key names that legitimately stay in Latin letters inside any language.</summary>
    private static readonly string[] LatinAllowed =
    {
        "Ctrl", "Shift", "Alt", "TAOM", "Bannerlord", "NativeSkinFixes", "covers_head", "Discord", "Steam",
        "MCM", "Harmony", "Patreon", "Nexus", "Workshop", "Modding", "Kit",
    };

    /// <summary>Placeholders, markup, conversation animation tags and a leading [culture] tag.</summary>
    private static readonly System.Text.RegularExpressions.Regex Markup = new System.Text.RegularExpressions.Regex(
        @"\{[^}]*\}|<[^>]*>|\[[a-z]{2}:[^\]]*\]|^\[[^\]]*\]");

    private static readonly System.Text.RegularExpressions.Regex Word =
        new System.Text.RegularExpressions.Regex(@"[\w-]+");

    private static readonly System.Text.RegularExpressions.Regex LatinRun =
        new System.Text.RegularExpressions.Regex(@"[A-Za-zÀ-ɏ]+");

    internal static string Script(char c)
    {
        if (c >= 0x0400 && c <= 0x04FF) return "Cyrillic";
        if ((c >= 0xAC00 && c <= 0xD7AF) || (c >= 0x1100 && c <= 0x11FF) || (c >= 0x3130 && c <= 0x318F)) return "Hangul";
        if ((c >= 0x3040 && c <= 0x30FF) || (c >= 0x31F0 && c <= 0x31FF)) return "Kana";
        if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0xF900 && c <= 0xFAFF)) return "Han";
        if (char.IsLetter(c) && (c < 0x0250 || (c >= 0x1E00 && c <= 0x1EFF))) return "Latin";
        return null;
    }

    /// <summary>What is wrong with one row's text for one language, or an empty list.</summary>
    internal static List<string> WritingSystemProblems(string language, string text)
    {
        // The katakana middle dot is the standard separator in Chinese transliterated names.
        var body = Markup.Replace(text ?? string.Empty, " ").Replace('・', ' ');
        var found = new List<string>();
        var scripts = new HashSet<string>(body.Select(Script).Where(s => s != null));
        if (LatinScriptLanguages.Contains(language))
        {
            var foreign = scripts.Where(s => s != "Latin").ToList();
            if (foreign.Count > 0)
            {
                found.Add("foreign script " + string.Join("/", foreign.OrderBy(s => s)));
            }
            return found;
        }
        if (!ForeignScripts.TryGetValue(language, out var banned))
        {
            return found;
        }
        var present = banned.Where(scripts.Contains).ToList();
        if (present.Count > 0)
        {
            found.Add("foreign script " + string.Join("/", present));
        }
        // Every word rule below needs a Latin letter in the word, and every word is a slice of body.
        if (!scripts.Contains("Latin"))
        {
            return found;
        }
        foreach (System.Text.RegularExpressions.Match w in Word.Matches(body))
        {
            var token = w.Value;
            if (LatinAllowed.Any(a => token.Contains(a)))
            {
                continue;
            }
            var tokenScripts = new HashSet<string>(token.Select(Script).Where(s => s != null));
            var latinOnlyLowercaseWord = tokenScripts.SetEquals(new[] { "Latin" }) && token.Length >= 4 && token == token.ToLowerInvariant();
            if (language == "RU")
            {
                if (tokenScripts.Contains("Latin") && tokenScripts.Contains("Cyrillic"))
                    found.Add($"mixed word '{token}'");
                else if (latinOnlyLowercaseWord)
                    found.Add($"English word '{token}'");
                continue;
            }
            // Chinese, Japanese, Korean: a lowercase-initial Latin run glued to the native script is a
            // split word ("黑numenor"); a capitalized name followed by a particle is style, not damage.
            // Judged per hyphen-separated part: in "Bahr al-Yeshm이라" the lowercase "al" belongs to the
            // name, not to the Hangul particle. The English-word rule keeps the whole hyphenated word, so a
            // kept Latin name such as "Cigfran-lûth" is not read as the English word "lûth".
            var split = tokenScripts.Count > 1 && token.Split('-').Any(part =>
                part.Select(Script).Where(s => s != null).Distinct().Count() > 1
                && LatinRun.Matches(part).Cast<System.Text.RegularExpressions.Match>()
                    .Any(r => r.Value.Length >= 2 && char.IsLower(r.Value[0])));
            if (split)
                found.Add($"mixed word '{token}'");
            else if (latinOnlyLowercaseWord)
                found.Add($"English word '{token}'");
        }
        return found;
    }

    [DataTestMethod]
    [DataRow("TR", "Vahşi돌진")]
    [DataRow("TR", "Özgür氏族 Halkı")]
    [DataRow("SP", "Mariscal de los Bардингos")]
    [DataRow("RU", "Капитан чёрных uruков")]
    [DataRow("RU", "Обучался в зале короля Брандa")]
    [DataRow("RU", "Бûрзгâш")]
    [DataRow("RU", "[Ривенделл]新obranец Имладриса")]
    [DataRow("CNs", "[魔多] 黑numenor轻型马铠 I")]
    [DataRow("KO", "이ム라드리스의 수호자")]
    [DataRow("JP", "その背から槍と穂先で discharge——突き伏せる")]
    public void WritingSystemProblems_DamagedRow_IsReported(string language, string text)
    {
        Assert.AreNotEqual(0, WritingSystemProblems(language, text).Count, text);
    }

    [DataTestMethod]
    [DataRow("TR", "Vahşi Hamle")]
    [DataRow("RU", "Друэдайн, известные людям как во́зы")]
    [DataRow("RU", "Удерживайте Ctrl вместе с этой клавишей")]
    [DataRow("CNt", "諾斯・佩瑞赫爾")]
    [DataRow("JP", "NativeSkinFixes有効 — covers_headモーフ修正")]
    [DataRow("KO", "Ain Baliq은 Jarjara 절벽지대")]
    [DataRow("CNs", "是的。[ib:hip][if:convo_excited]{ENEMYFACTION_INFORMALNAME}")]
    [DataRow("DE", "[Dol Guldur] Goblin-Bogenschütze")]
    [DataRow("KO", "Bahr al-Yeshm이라")]
    [DataRow("CNt", "[登蘭德] Cigfran-lûth 弓箭手")]
    public void WritingSystemProblems_CleanRow_IsNotReported(string language, string text)
    {
        var problems = WritingSystemProblems(language, text);
        Assert.AreEqual(0, problems.Count, string.Join("; ", problems));
    }

    [TestMethod]
    public void NoTranslatedString_MixesWritingSystems()
    {
        var files = Directory.GetFiles(LanguagesPath, "std_taom_*.xml", SearchOption.AllDirectories);
        Assert.IsTrue(files.Length > 100, $"Only {files.Length} language files found; the scan is broken.");

        var offenders = new List<string>();
        foreach (var file in files)
        {
            var language = Path.GetFileName(Path.GetDirectoryName(file));
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }
            foreach (var row in doc.Descendants("string"))
            {
                var problems = WritingSystemProblems(language, (string)row.Attribute("text"));
                if (problems.Count > 0)
                {
                    offenders.Add($"  {language}/{Path.GetFileName(file)} [{(string)row.Attribute("id")}] {problems[0]}");
                }
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Translations carry words from the wrong writing system (a translator-model failure the " +
            "placeholder check cannot see):\n" + string.Join("\n", offenders.Take(60)) +
            "\n\nReset each row to its English, drop its key from tools/translation_cache/<lang>.json, " +
            "and re-run the translator, or correct row and cache together by hand.");
    }

    /// <summary>The cache file name of each language directory (tools/translate_with_claude.py).</summary>
    private static readonly Dictionary<string, string> CacheFileLanguage = new Dictionary<string, string>
    {
        ["br"] = "BR", ["cns"] = "CNs", ["cnt"] = "CNt", ["de"] = "DE", ["fr"] = "FR", ["it"] = "IT",
        ["jp"] = "JP", ["ko"] = "KO", ["pl"] = "PL", ["ru"] = "RU", ["sp"] = "SP", ["tr"] = "TR",
    };

    [TestMethod]
    public void NoCachedTranslation_MixesWritingSystems()
    {
        // The cache is the translator's output for all three modules, the live Armory and TAOM_Map
        // included, which no repo test can read; and it is what brings damage back on the next run.
        var cacheDir = Path.Combine(RepoRoot, "tools", "translation_cache");
        var files = Directory.GetFiles(cacheDir, "*.json");
        Assert.AreEqual(CacheFileLanguage.Count, files.Length, $"expected one cache per language in {cacheDir}");

        var offenders = new List<string>();
        var values = 0;
        foreach (var file in files)
        {
            var language = CacheFileLanguage[Path.GetFileNameWithoutExtension(file)];
            var cache = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file));
            foreach (var entry in cache)
            {
                values++;
                var problems = WritingSystemProblems(language, entry.Value);
                if (problems.Count > 0)
                {
                    offenders.Add($"  {Path.GetFileName(file)} [{entry.Key}] {problems[0]}");
                }
            }
        }

        Assert.IsTrue(values > 100000, $"Only {values} cached translations read; the scan is broken.");
        Assert.AreEqual(0, offenders.Count,
            "The translation cache carries words from the wrong writing system; the next translator run " +
            "would write them back into the repo, the Armory or TAOM_Map:\n" + string.Join("\n", offenders.Take(60)) +
            "\n\nCorrect the cache entry and every row that carries it.");
    }
}
