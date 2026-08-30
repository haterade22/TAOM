using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Runs lords.xslt and heroes.xslt for real, over the vanilla files they overlay, and checks the
/// family graph the engine actually ends up with.
///
/// Why this exists on top of LordIdentityConsistencyTests: a heroes.xslt template that does not
/// strip an attribute INHERITS vanilla's value, and nothing in the repo records what that value
/// is. Reading the markup cannot see it. Eight defects were invisible that way on 2026-08-29,
/// every one of them a family the repo never mentions: Duilin married to the id the template makes
/// his mother, Erkenbrand married to his own son, Grimbold's four children with a female father
/// and a male mother, Anariel still married to the father the template had just given her, and
/// Maireas the Variag warlord flagged female while married to her own wife.
///
/// This is the sentinel-transform shape from .claude/rules/tests.md, except the input is the real
/// vanilla document rather than a synthetic stub, because vanilla inheritance is the whole point.
/// It skips rather than fails when the game install is absent.
/// </summary>
[TestClass]
public class LordFamilyTransformTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    private sealed class Person
    {
        public string Id = "";
        public string Name = "";
        public bool IsFemale;
        public string? Father;
        public string? Mother;
        public string? Spouse;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string? SandBoxModuleData()
    {
        var game = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        foreach (var root in new[] { game, DefaultGameDir })
        {
            if (string.IsNullOrEmpty(root)) continue;
            var path = Path.Combine(root, "Modules", "SandBox", "ModuleData");
            if (File.Exists(Path.Combine(path, "heroes.xml")) && File.Exists(Path.Combine(path, "lords.xml")))
                return path;
        }
        return null;
    }

    /// <summary>Applies a stylesheet to a document the way MBObjectManager.ApplyXslt does.</summary>
    private static XDocument Transform(string sourceXml, string stylesheet)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        var xslt = new XslCompiledTransform();
        using (var sheet = XmlReader.Create(stylesheet, settings))
            xslt.Load(sheet, new XsltSettings(false, false), null);

        using var input = XmlReader.Create(sourceXml, settings);
        using var buffer = new StringWriter();
        using (var output = XmlWriter.Create(buffer, new XmlWriterSettings { ConformanceLevel = ConformanceLevel.Auto }))
            xslt.Transform(input, output);
        return XDocument.Parse(buffer.ToString());
    }

    private static string Strip(string? value) =>
        string.IsNullOrEmpty(value) ? "" : Regex.Replace(value!, @"^\{=[^}]*\}", "");

    private static string? Hero(string? value) =>
        string.IsNullOrEmpty(value) ? null
        : value!.StartsWith("Hero.", StringComparison.Ordinal) ? value.Substring(5) : value;

    /// <summary>
    /// The roster the engine ends up with. SubModule.xml loads lords.xslt at :96 and heroes.xslt
    /// at :106, then characters/heroes.xml at :148 and characters/lords.xml at :157, so the plain
    /// XML files win on any duplicate id.
    /// </summary>
    private static Dictionary<string, Person> BuildRoster(string root, string vanilla)
    {
        var people = new Dictionary<string, Person>(StringComparer.Ordinal);
        Person At(string id) =>
            people.TryGetValue(id, out var p) ? p : people[id] = new Person { Id = id };

        foreach (var n in Transform(Path.Combine(vanilla, "lords.xml"),
                                    Path.Combine(root, "Main", "_Module", "ModuleData", "lords.xslt"))
                          .Descendants("NPCCharacter"))
        {
            var id = (string?)n.Attribute("id");
            if (id == null) continue;
            var p = At(id);
            p.Name = Strip((string?)n.Attribute("name"));
            p.IsFemale = string.Equals((string?)n.Attribute("is_female"), "true", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var n in Transform(Path.Combine(vanilla, "heroes.xml"),
                                    Path.Combine(root, "Main", "_Module", "ModuleData", "heroes.xslt"))
                          .Descendants("Hero"))
        {
            var id = (string?)n.Attribute("id");
            if (id == null) continue;
            var p = At(id);
            p.Father = Hero((string?)n.Attribute("father"));
            p.Mother = Hero((string?)n.Attribute("mother"));
            p.Spouse = Hero((string?)n.Attribute("spouse"));
        }

        // MBObjectManager.MergeElementAttributes overwrites only the attributes the later file
        // DECLARES; an attribute it omits survives from the accumulated document. So assign each
        // field only when characters/lords.xml actually states it, or a lord whose sex is set by
        // lords.xslt and left unstated here would be read as male when the engine makes her female.
        var moduleData = Path.Combine(root, "Main", "_Module", "ModuleData");
        foreach (var n in XDocument.Load(Path.Combine(moduleData, "characters", "lords.xml")).Descendants("NPCCharacter"))
        {
            var id = (string?)n.Attribute("id");
            if (id == null) continue;
            var p = At(id);
            if (n.Attribute("name") != null) p.Name = Strip((string?)n.Attribute("name"));
            if (n.Attribute("is_female") != null)
                p.IsFemale = string.Equals((string?)n.Attribute("is_female"), "true", StringComparison.OrdinalIgnoreCase);
        }
        foreach (var n in XDocument.Load(Path.Combine(moduleData, "characters", "heroes.xml")).Descendants("Hero"))
        {
            var id = (string?)n.Attribute("id");
            if (id == null) continue;
            var p = At(id);
            p.Father = Hero((string?)n.Attribute("father"));
            p.Mother = Hero((string?)n.Attribute("mother"));
            p.Spouse = Hero((string?)n.Attribute("spouse"));
        }

        return people;
    }

    [TestMethod]
    public void TheFamilyGraphTheEngineComputesIsCoherent()
    {
        var vanilla = SandBoxModuleData();
        if (vanilla == null)
            Assert.Inconclusive("Bannerlord install not found; the overlays have nothing to run against here.");

        var roster = BuildRoster(FindRepoRoot(), vanilla!);
        Assert.IsTrue(roster.Count > 1000, $"built a roster of only {roster.Count}; the transform or the file shapes changed");

        var faults = new List<string>();
        foreach (var p in roster.Values.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            string Who(string id) => roster.TryGetValue(id, out var q) && q.Name.Length > 0 ? $"{id} \"{q.Name}\"" : id;

            if (p.Spouse != null && p.Spouse == p.Father)
                faults.Add($"  {Who(p.Id)}: {Who(p.Spouse)} is both father and husband");
            if (p.Spouse != null && p.Spouse == p.Mother)
                faults.Add($"  {Who(p.Id)}: {Who(p.Spouse)} is both mother and wife");

            if (p.Father != null && roster.TryGetValue(p.Father, out var father) && father.IsFemale)
                faults.Add($"  {Who(p.Id)}: father {Who(p.Father)} is female");
            if (p.Mother != null && roster.TryGetValue(p.Mother, out var mother) && !mother.IsFemale)
                faults.Add($"  {Who(p.Id)}: mother {Who(p.Mother)} is male");

            if (p.Spouse != null && roster.TryGetValue(p.Spouse, out var spouse))
            {
                if (spouse.IsFemale == p.IsFemale)
                    faults.Add($"  {Who(p.Id)} and {Who(p.Spouse)} are married and both {(p.IsFemale ? "female" : "male")}");
                if (spouse.Spouse != null && spouse.Spouse != p.Id)
                    faults.Add($"  {Who(p.Id)} is married to {Who(p.Spouse)}, who is married to {Who(spouse.Spouse)}");
            }
        }

        Assert.AreEqual(0, faults.Count,
            "The family graph the engine computes is inconsistent. Read these against vanilla\n" +
            "SandBox/ModuleData/heroes.xml: a heroes.xslt template that does not strip father,\n" +
            "mother or spouse keeps vanilla's, and vanilla's is about a different character.\n" +
            string.Join("\n", faults));
    }
}
