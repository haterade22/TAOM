using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.CoopCompat.Tests;

/// <summary>
/// ADR-002 for this assembly, the intent of TAOM.Tests' EntryPointLineCountGateTests: a submodule,
/// campaign behavior or mission behavior stays under 150 lines, so logic lives in a service. Read from
/// the source text, like the original; there is no baseline because this assembly starts clean.
/// A class that derives from another class declared in the same tree counts too.
/// </summary>
[TestClass]
public class EntryPointLineCountGateTests
{
    private const int Limit = 150;

    private static readonly HashSet<string> RootBases = new(StringComparer.Ordinal)
    {
        "MBSubModuleBase", "CampaignBehaviorBase", "MissionBehavior", "MissionLogic", "MissionView",
    };

    private static readonly Regex ClassDeclaration = new(@"\bclass\s+(\w+)(?:\s*<[^>]*>)?\s*:\s*([\w.]+)", RegexOptions.Compiled);

    internal static List<string> Offenders(IEnumerable<KeyValuePair<string, string>> sources)
    {
        var declared = new List<(string Name, string Base, string File, int Lines)>();
        foreach (var source in sources)
        {
            var lines = source.Value.Split('\n').Length;
            foreach (Match m in ClassDeclaration.Matches(source.Value))
                declared.Add((m.Groups[1].Value, m.Groups[2].Value.Split('.').Last(), source.Key, lines));
        }

        var entryPoints = new HashSet<string>(RootBases, StringComparer.Ordinal);
        bool grew;
        do
        {
            grew = false;
            foreach (var d in declared.Where(d => entryPoints.Contains(d.Base) && !entryPoints.Contains(d.Name)))
                grew |= entryPoints.Add(d.Name);
        }
        while (grew);

        return declared.Where(d => entryPoints.Contains(d.Name) && d.Lines >= Limit)
            .Select(d => d.Name + " in " + d.File + ": " + d.Lines + " lines (limit " + Limit + "); move the logic into a service (ADR-002)")
            .ToList();
    }

    [TestMethod]
    public void EntryPoints_OfTheCoopCompatModule_StayUnder150Lines()
    {
        var root = RepoFiles.Path("CoopCompat");
        var sources = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Select(f => new KeyValuePair<string, string>(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();

        Assert.IsTrue(sources.Any(s => s.Key == "CoopCompatSubModule.cs"), "the scan found the module's sources");
        CollectionAssert.AreEqual(new List<string>(), Offenders(sources));
    }

    [TestMethod]
    public void Scan_FindsADirectAndADerivedEntryPoint_AndIgnoresOtherClasses()
    {
        string Filler(int n) => string.Concat(Enumerable.Repeat("int x;\n", n));
        var sources = new List<KeyValuePair<string, string>>
        {
            new("Sub.cs", "class BigSub : MBSubModuleBase\n{\n" + Filler(160) + "}\n"),
            new("Mid.cs", "class Mid : CampaignBehaviorBase { }\n"),
            new("Leaf.cs", "class Leaf : Mid\n{\n" + Filler(160) + "}\n"),
            new("Svc.cs", "class BigService : object\n{\n" + Filler(160) + "}\n"),
            new("Small.cs", "class SmallSub : MBSubModuleBase\n{\n" + Filler(20) + "}\n"),
        };

        var names = Offenders(sources).Select(o => o.Split(' ')[0]).OrderBy(n => n, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(new[] { "BigSub", "Leaf" }, names);
    }
}
