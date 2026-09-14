using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Pins that <c>comment_strings.xslt</c> preserves each overridden string's <c>&lt;tags&gt;</c>
/// children.
///
/// <para>
/// The engine uses <c>&lt;tags&gt;</c> to choose which variant of a conversation line applies
/// (culture, persona, trait). <c>&lt;xsl:copy&gt;</c> copies the element but NOT its children, and
/// these templates follow it with an attribute-only <c>&lt;xsl:apply-templates
/// select="@*[...]"/&gt;</c>. Without an explicit <c>&lt;xsl:apply-templates select="node()"/&gt;</c>
/// the override silently emits a tag-stripped string, which stops matching the case it was written
/// for and matches every culture at once instead.
/// </para>
///
/// <para>
/// This is the exact "passthrough inherits, it does not preserve" defect class that
/// <c>.claude/rules/vanilla-data-comparison.md</c> warns about: the bug is an ABSENCE, so there is
/// nothing to grep for and reading the stylesheet does not reveal it. It has to be caught by
/// asserting on the transform's OUTPUT.
/// </para>
///
/// <para>
/// History: 12 of the 35 override templates carried the fix and a comment explaining it. The other
/// 23 did not, and silently stripped tags until the v1.5.0 bump audit measured the output. This
/// test exists so that partial state cannot recur.
/// </para>
///
/// Uses the sentinel-stub pattern from <c>.claude/rules/tests.md</c>: a synthetic input document
/// rather than the installed vanilla file, so the test is deterministic and needs no game install.
/// </summary>
[TestClass]
public class CommentStringTagsTests
{
    private static string XsltPath() =>
        Path.Combine(CultureDataFixture.ModuleDataPath(), "comment_strings.xslt");

    // Every string id the stylesheet overrides, read from the stylesheet itself so the test cannot
    // drift out of sync with it: a newly added override is covered automatically.
    private static List<string> OverriddenIds()
    {
        var xslt = File.ReadAllText(XsltPath());
        return Regex.Matches(xslt, @"match=""string\[@id='([^']+)'\]""")
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Distinct()
                    .ToList();
    }

    private static XDocument SentinelStub(IEnumerable<string> ids)
    {
        var root = new XElement("base", new XAttribute("type", "string"));
        var strings = new XElement("strings");
        foreach (var id in ids)
        {
            strings.Add(new XElement("string",
                new XAttribute("id", id),
                new XAttribute("text", "SENTINEL_TEXT"),
                new XElement("tags",
                    new XElement("tag", new XAttribute("id", "SENTINEL_TAG")))));
        }
        root.Add(strings);
        return new XDocument(root);
    }

    [TestMethod]
    public void CommentStringsXslt_PreservesTagsOnEveryOverriddenString()
    {
        var ids = OverriddenIds();
        Assert.IsTrue(ids.Count > 0, "No override templates found in comment_strings.xslt.");

        var transform = new XslCompiledTransform();
        transform.Load(XsltPath());

        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(SentinelStub(ids).CreateReader(), null, writer);

        var emitted = output.Descendants("string")
                            .ToDictionary(s => (string)s.Attribute("id"), s => s);

        var stripped = new List<string>();
        var notOverridden = new List<string>();
        foreach (var id in ids)
        {
            Assert.IsTrue(emitted.ContainsKey(id), $"'{id}' vanished from the transform output.");
            var el = emitted[id];

            // The whole point of the template is to replace the text, so a surviving sentinel means
            // the override never bound and the real run would inherit vanilla's line.
            if ((string)el.Attribute("text") == "SENTINEL_TEXT") notOverridden.Add(id);

            // The defect under test: children dropped, so the variant selector is gone.
            if (!el.Elements("tags").Any()) stripped.Add(id);
        }

        Assert.AreEqual(0, stripped.Count,
            "These overridden strings lost their <tags>, so they no longer match the culture or "
            + "persona they were written for and match everything at once instead. Add "
            + "<xsl:apply-templates select=\"node()\"/> after the <xsl:attribute> in each template:"
            + System.Environment.NewLine + string.Join(System.Environment.NewLine, stripped));

        Assert.AreEqual(0, notOverridden.Count,
            "These templates matched but did not replace the text:"
            + System.Environment.NewLine + string.Join(System.Environment.NewLine, notOverridden));
    }
}
