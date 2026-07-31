using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins the parsing contract for <c>coop-modules.txt</c> — the shipped, user-editable list of
/// co-op module ids and extra Harmony owner prefixes that <see cref="CoopPresence"/> reads.
///
/// The load-bearing invariant is UNION, NOT REPLACE. The parsed file feeds
/// <c>PatchShield.ProtectedOwnerPrefixes</c>, so a corrupt, truncated or maliciously-edited file
/// must never be able to REMOVE a compiled default — that would unprotect the BUTR/MCM stack and
/// let PatchShield unpatch it on the first missing-API exception. Every test below exists to make
/// that impossible rather than merely unlikely.
/// </summary>
[TestClass]
public class CoopModuleListTests
{
    private static readonly string[] ModuleDefaults = { "BannerlordTogether", "BattleLinkMPClient" };
    private static readonly string[] OwnerDefaults = { "TAOM", "Bannerlord.ButterLib" };

    private static CoopModuleListResult Parse(params string[] lines) =>
        CoopModuleList.Parse(lines, ModuleDefaults, OwnerDefaults);

    [TestMethod]
    public void Parse_NullLines_ReturnsCompiledDefaults()
    {
        var result = CoopModuleList.Parse(null, ModuleDefaults, OwnerDefaults);

        CollectionAssert.AreEquivalent(ModuleDefaults, result.ModuleIds.ToArray());
        CollectionAssert.AreEquivalent(OwnerDefaults, result.OwnerPrefixes.ToArray());
    }

    [TestMethod]
    public void Parse_EmptyFile_ReturnsCompiledDefaults()
    {
        var result = Parse();

        CollectionAssert.AreEquivalent(ModuleDefaults, result.ModuleIds.ToArray());
        CollectionAssert.AreEquivalent(OwnerDefaults, result.OwnerPrefixes.ToArray());
    }

    [TestMethod]
    public void Parse_GarbageOnly_StillContainsEveryCompiledDefault()
    {
        var result = Parse("\0\0\0", "]][[not a section", "   ", "\t\t");

        foreach (var expected in ModuleDefaults)
            Assert.IsTrue(result.ModuleIds.Contains(expected), $"module default '{expected}' was lost");
        foreach (var expected in OwnerDefaults)
            Assert.IsTrue(result.OwnerPrefixes.Contains(expected), $"owner default '{expected}' was lost");
    }

    [TestMethod]
    public void Parse_ModulesSection_AddsEntriesOnTopOfDefaults()
    {
        var result = Parse("[modules]", "SomeOtherCoopMod");

        Assert.IsTrue(result.ModuleIds.Contains("SomeOtherCoopMod"));
        Assert.IsTrue(result.ModuleIds.Contains("BannerlordTogether"));
    }

    [TestMethod]
    public void Parse_OwnerPrefixSection_AddsEntriesOnTopOfDefaults()
    {
        var result = Parse("[harmony-owner-prefixes]", "com.example.coop");

        Assert.IsTrue(result.OwnerPrefixes.Contains("com.example.coop"));
        Assert.IsTrue(result.OwnerPrefixes.Contains("TAOM"));
    }

    [TestMethod]
    public void Parse_NoSectionHeader_TreatsEntriesAsModuleIds()
    {
        // A user who just lists ids without a header gets the obvious behaviour.
        var result = Parse("SomeOtherCoopMod");

        Assert.IsTrue(result.ModuleIds.Contains("SomeOtherCoopMod"));
        Assert.IsFalse(result.OwnerPrefixes.Contains("SomeOtherCoopMod"));
    }

    [TestMethod]
    public void Parse_CommentsAndBlankLines_AreIgnored()
    {
        var result = Parse("# a comment", "", "   ", "[modules]", "  # indented comment", "RealMod  ");

        Assert.IsTrue(result.ModuleIds.Contains("RealMod"), "trailing whitespace should be trimmed");
        Assert.IsFalse(result.ModuleIds.Any(id => id.StartsWith("#")));
        Assert.IsFalse(result.ModuleIds.Any(string.IsNullOrWhiteSpace));
    }

    [TestMethod]
    public void Parse_DuplicateEntries_CollapseCaseInsensitively()
    {
        var result = Parse("[modules]", "bannerlordtogether", "BANNERLORDTOGETHER", "BannerlordTogether");

        Assert.AreEqual(1, result.ModuleIds.Count(id =>
            string.Equals(id, "BannerlordTogether", System.StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void Parse_UnknownSection_IgnoresItsEntriesButKeepsDefaults()
    {
        var result = Parse("[not-a-real-section]", "JunkEntry");

        Assert.IsFalse(result.ModuleIds.Contains("JunkEntry"));
        Assert.IsFalse(result.OwnerPrefixes.Contains("JunkEntry"));
        Assert.IsTrue(result.ModuleIds.Contains("BannerlordTogether"));
    }

    [TestMethod]
    public void Parse_ExceedsEntryCap_TruncatesFileEntriesButKeepsEveryDefault()
    {
        var lines = new[] { "[modules]" }
            .Concat(Enumerable.Range(0, CoopModuleList.MaxEntriesPerSection + 50).Select(i => $"Mod{i}"))
            .ToArray();

        var result = CoopModuleList.Parse(lines, ModuleDefaults, OwnerDefaults);

        Assert.IsTrue(result.Truncated, "a file over the cap must report truncation");
        foreach (var expected in ModuleDefaults)
            Assert.IsTrue(result.ModuleIds.Contains(expected),
                $"truncation must never drop the compiled default '{expected}'");
    }

    [TestMethod]
    public void Parse_EntryMatchingCompiledDefault_DoesNotDuplicateIt()
    {
        var result = Parse("[harmony-owner-prefixes]", "TAOM");

        Assert.AreEqual(1, result.OwnerPrefixes.Count(p =>
            string.Equals(p, "TAOM", System.StringComparison.OrdinalIgnoreCase)));
    }
}
