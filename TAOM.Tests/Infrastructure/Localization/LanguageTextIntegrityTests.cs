using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// No shipped translation may contain a character that is itself a decoding failure.
///
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
/// Scoped to the replacement character and C0 controls deliberately. Every other non-ASCII
/// codepoint in these files is legitimate — the twelve languages include four non-Latin
/// scripts, and a broad "suspicious character" rule would report thousands of correct rows.
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
}
