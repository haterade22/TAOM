using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PlayerSwitcher.UI;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. Gauntlet does not report a missing binding. A prefab that names a property the
/// ViewModel does not have simply renders nothing there, forever, with no log line and no
/// exception, so a rename is invisible until someone opens the screen in game. This test reads the
/// shipped prefab and proves every name in it resolves.
///
/// The prefab predates this feature: it crossed over from LOTRAOM unchanged and is used as-is, so
/// the ViewModels are written to fit the file rather than the other way round.
/// </summary>
[TestClass]
public class PlayerSwitcherPrefabContractTests
{
    private const string PrefabRelativePath =
        @"Main\_Module\GUI\Prefabs\FacGen\PreBuildCharacterSelection.xml";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ReadPrefab()
    {
        var path = Path.Combine(FindRepoRoot(), PrefabRelativePath);
        Assert.IsTrue(File.Exists(path), $"picker prefab not found at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>Everything inside an ItemTemplate binds against the row VM, not the panel VM.</summary>
    private static (string outer, string itemTemplates) SplitByScope(string xml)
    {
        var templates = Regex.Matches(xml, @"<ItemTemplate>(.*?)</ItemTemplate>", RegexOptions.Singleline)
            .Cast<Match>().Select(m => m.Value).ToList();

        var outer = templates.Aggregate(xml, (acc, t) => acc.Replace(t, string.Empty));
        return (outer, string.Join(Environment.NewLine, templates));
    }

    private static IEnumerable<string> DataSources(string xml)
        => Regex.Matches(xml, @"DataSource=""\{([A-Za-z_][A-Za-z0-9_]*)\}""")
            .Cast<Match>().Select(m => m.Groups[1].Value).Distinct();

    private static IEnumerable<string> TextBindings(string xml)
        => Regex.Matches(xml, @"Text=""@([A-Za-z_][A-Za-z0-9_]*)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).Distinct();

    private static IEnumerable<string> AtBindings(string xml)
        => Regex.Matches(xml, @"\b(?:IsSelected|IsChild|IsVisible|IsEnabled)=""@([A-Za-z_][A-Za-z0-9_]*)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).Distinct();

    private static IEnumerable<string> Commands(string xml)
        => Regex.Matches(xml, @"Command\.[A-Za-z]+=""([A-Za-z_][A-Za-z0-9_]*)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).Distinct();

    private static bool HasPublicMember(Type type, string name)
        => type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public) != null
           || type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public) != null
           || type.GetField(name, BindingFlags.Instance | BindingFlags.Public) != null;

    private static void AssertAllResolve(IEnumerable<string> names, Type type, string what)
    {
        var missing = names.Where(n => !HasPublicMember(type, n)).ToArray();
        Assert.AreEqual(0, missing.Length,
            $"{what} bound by the prefab but absent from {type.Name}: {string.Join(", ", missing)}. " +
            "Gauntlet renders nothing for a missing binding and says nothing about it.");
    }

    [TestMethod]
    public void EveryPanelBindingResolvesOnThePanelViewModel()
    {
        var (outer, _) = SplitByScope(ReadPrefab());

        var names = DataSources(outer).Concat(TextBindings(outer)).Concat(AtBindings(outer)).Concat(Commands(outer));

        AssertAllResolve(names, typeof(PlayerSwitcherVM), "panel members");
    }

    [TestMethod]
    public void EveryRowBindingResolvesOnTheRowViewModel()
    {
        var (_, templates) = SplitByScope(ReadPrefab());
        Assert.IsTrue(templates.Length > 0, "the prefab should declare item templates for the three lists");

        var names = TextBindings(templates).Concat(AtBindings(templates)).Concat(Commands(templates));

        AssertAllResolve(names, typeof(HeroPickItemVM), "row members");
    }

    [TestMethod]
    public void ThePrefabStillBindsTheThreeGroupsTheServiceProduces()
    {
        var (outer, _) = SplitByScope(ReadPrefab());
        var sources = DataSources(outer).ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "KingdomMembers", "ClanLeaders", "Companions" }, sources,
            "the three lists are the feature's whole surface; a rename here silently empties a group");
    }

    [TestMethod]
    public void TheRowViewModelAlsoSatisfiesTheVanillaTupleItInherits()
    {
        // ClanLordTuple.xml (SandBox/GUI/Prefabs/Clan/Management) binds these against whatever
        // sits in the item slot. ClanPartyMemberItemVM supplies Name, Visual, Banner_9,
        // ExecuteLink, ExecuteBeginHint and ExecuteEndHint; the four below it does not supply, so
        // HeroPickItemVM must, or the row logs nothing and renders half-empty.
        foreach (var member in new[] { "IsSelected", "IsChild", "CurrentActionText", "OnCharacterSelect" })
        {
            Assert.IsTrue(HasPublicMember(typeof(HeroPickItemVM), member),
                $"HeroPickItemVM must supply '{member}', which the vanilla ClanLordTuple binds and the base VM does not");
        }

        foreach (var inherited in new[] { "Name", "Visual", "Banner_9", "ExecuteLink", "ExecuteBeginHint", "ExecuteEndHint" })
        {
            Assert.IsTrue(HasPublicMember(typeof(HeroPickItemVM), inherited),
                $"'{inherited}' should come from ClanPartyMemberItemVM; if it does not, the base class changed");
        }
    }

    [TestMethod]
    public void BothClickHandlersExist_SoTheOuterVersusInnerQuestionCannotBiteUs()
    {
        // The TAOM prefab puts Command.Click on the <ClanLordTuple> element itself, while the
        // tuple's own inner ButtonWidget already binds Command.Click="OnCharacterSelect". Which
        // one wins is a Gauntlet routing detail that cannot be settled offline, so both handlers
        // exist and both do the same thing.
        Assert.IsTrue(HasPublicMember(typeof(HeroPickItemVM), "OnPreBuildCharacterSelected"));
        Assert.IsTrue(HasPublicMember(typeof(HeroPickItemVM), "OnCharacterSelect"));
    }
}
