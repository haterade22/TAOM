using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// An XML comment inside a culture's child list crashes campaign start.
///
/// <para>
/// The engine walks <c>notable_templates</c> by child NODE, not by child ELEMENT, and hands each
/// one to <c>MBObjectManager.ReadObjectReferenceFromXml("name", node)</c>. That reads
/// <c>node.Attributes["name"]</c>, and <c>XmlComment.Attributes</c> is <c>null</c>, so a comment
/// sitting between two <c>&lt;template&gt;</c> entries throws <c>NullReferenceException</c> inside
/// <c>TaleWorlds.ObjectSystem</c> and aborts the parse of the ENTIRE file.
/// </para>
///
/// <para>
/// The blast radius is the reason this is a gate rather than a style rule. The abort is silent from
/// the mod's side and every culture defined AFTER the comment simply never registers, so the visible
/// failure is a second, unrelated-looking crash somewhere else: <c>Kingdom.InitializeKingdom</c>
/// throwing on <c>empire_w</c> because <c>Culture.gondor</c>, defined 2,400 lines further down the
/// same file, does not exist. Shipped in v2.1.0 and fixed in v2.1.1.
/// </para>
///
/// <para>
/// Comments are NOT banned outright: 61 sit on the root and 4 directly inside <c>&lt;Culture&gt;</c>
/// and have shipped for months, because those loaders match on element name. The rule is positional,
/// so the allowed set is exactly the placements with a shipping track record.
/// </para>
///
/// <para>
/// Why the sibling gender gate could not catch this: it queries with
/// <c>Descendants("template")</c>, and LINQ-to-XML skips comment nodes for the same reason the
/// engine's own element-name loaders do. A test written in the query style of the thing it checks
/// inherits that thing's blind spot.
/// </para>
/// </summary>
[TestClass]
public class CultureXmlCommentPlacementTests
{
    // Placements with a shipping track record. Anything deeper is a child list the engine may walk
    // by node.
    private static readonly HashSet<string> CommentSafeParents =
        new HashSet<string>(StringComparer.Ordinal) { "SPCultures", "Culture" };

    private static string CultureXmlPath() =>
        Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_spcultures.xml");

    [TestMethod]
    public void CultureXml_HasNoCommentInsideAChildList()
    {
        var doc = XDocument.Load(CultureXmlPath(), LoadOptions.SetLineInfo);

        var offenders = doc.Descendants()
                           .SelectMany(e => e.Nodes().OfType<XComment>()
                                             .Select(c => new { Parent = e, Comment = c }))
                           .Where(x => !CommentSafeParents.Contains(x.Parent.Name.LocalName))
                           .Select(x =>
                           {
                               var culture = x.Parent.AncestorsAndSelf()
                                              .FirstOrDefault(a => a.Name.LocalName == "Culture");
                               var id = (string)culture?.Attribute("id") ?? "?";
                               var line = (x.Comment as System.Xml.IXmlLineInfo)?.LineNumber ?? 0;
                               var text = x.Comment.Value.Trim().Replace("\r", " ").Replace("\n", " ");
                               if (text.Length > 60) text = text.Substring(0, 60) + "...";
                               return $"line {line}: <{x.Parent.Name.LocalName}> in culture '{id}' -> \"{text}\"";
                           })
                           .ToList();

        Assert.AreEqual(0, offenders.Count,
            "XML comments are sitting inside a culture child list. The engine walks these lists by "
            + "child NODE and calls MBObjectManager.ReadObjectReferenceFromXml on each one; "
            + "XmlComment.Attributes is null, so this throws inside TaleWorlds.ObjectSystem and "
            + "aborts the parse of the whole file. Every culture defined below the comment then "
            + "fails to register, and the crash you actually see is somewhere else entirely. "
            + "Move the comment out to the <Culture> element or the file root:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [TestMethod]
    public void CultureXml_StillHasTheFemaleNotableTemplatesTheCommentsDocumented()
    {
        // Guards the fix's blast radius in the other direction: deleting the offending comments must
        // not have taken the entries they annotated with them.
        var doc = XDocument.Load(CultureXmlPath());

        var listsWithoutFemale = doc.Descendants("notable_templates")
            .Where(nt => !nt.Elements("template")
                            .Any(t => ((string)t.Attribute("name") ?? string.Empty).EndsWith("_f",
                                       StringComparison.Ordinal)))
            .Select(nt => (string)nt.Parent?.Attribute("id") ?? "?")
            .ToList();

        Assert.AreEqual(0, listsWithoutFemale.Count,
            "These cultures lost the female notable template that the removed comments documented: "
            + string.Join(", ", listsWithoutFemale));
    }
}
