using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// The muster's captain comes only from troops the screen offers (#1118 review). TAOM redefines 172 vanilla and Fords
/// of War ids as obsolete; the troop list hides them, so the captain picker must too. CaptainResolver reads the live
/// module XML and the object manager, so these are source pins, comments stripped.
/// </summary>
[TestClass]
public class CaptainResolverSourceTests
{
    private const string Path = "Client/Features/CustomBattles/Muster/CaptainResolver.cs";

    [TestMethod]
    public void IsOffered_RejectsAnObsoleteTroopAndANonSoldier()
    {
        // The roster (TcbVM.SelectFaction) also requires IsSoldier; without it here the captain fallback could pick a
        // caravan guard, mercenary, bandit or villager the roster hides.
        var src = RepoPaths.ReadSource(Path, stripComments: true);
        var body = Regex.Match(src, @"public static bool IsOffered\(BasicCharacterObject c\)[\s\S]*?\n        \}").Value;

        Assert.IsTrue(body.Length > 0, "IsOffered not found");
        StringAssert.Matches(body, new Regex(@"if \(c == null \|\| !c\.IsSoldier \|\| c\.IsObsolete\) return false;"));
    }

    [TestMethod]
    public void Resolve_SkipsAnObsoleteUpgrade()
    {
        var src = RepoPaths.ReadSource(Path, stripComments: true);

        StringAssert.Matches(src, new Regex(@"\.Where\(c => c != null && !c\.IsObsolete\)\.ToList\(\)"));
    }
}
